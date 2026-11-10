using Modules.Music.Tracks;
using System.Buffers;
using System.Threading.Channels;
using Application.Discord.Voice;
using Modules.Music.Tracks.AudioSources;
using Modules.Music.Players;
using Microsoft.Extensions.Logging;
using Modules.Music.Processes;

namespace Modules.Music.Playback.Native;

internal sealed class NativePlaybackInstance : IPlaybackInstance
{
    private readonly CancellationTokenSource _cts;

    private readonly Task _playbackTask;
    private Volume _volume;
    private readonly object _pauseSync = new();
    private TaskCompletionSource _resumeSignal = CreateSignaledSignal();
    private bool _isPaused;
    private long _bytesWritten;
    private long _bytesProduced;
    private readonly TimeSpan _startPosition;

    private const int PcmBytesPerSecond = 48000 * 2 * sizeof(short);
    private const int ChunkSize = PcmBytesPerSecond / 50; // 20 ms of 48 kHz stereo PCM16
    private const int ChannelCapacity = 100; // ~2 seconds

    private readonly record struct PcmChunk(byte[] Buffer, int Length);

    private readonly Channel<PcmChunk> _channel;

    public Task Completion => _playbackTask;

    public TimeSpan Position => _startPosition + TimeSpan.FromSeconds(Interlocked.Read(ref _bytesWritten) / (double)PcmBytesPerSecond);

    public NativePlaybackInstance(
        IAudioSource source,
        Track track,
        Volume volume,
        IAudioTranscoder transcoder,
        IVoiceOutput voiceOutput,
        CancellationToken externalToken,
        bool isPaused = false,
        TimeSpan startPosition = default,
        ResolvedAudioSource? resolvedSource = null,
        ILogger<NativePlaybackInstance>? logger = null)
    {
        _volume = volume;
        _startPosition = startPosition;
        _isPaused = isPaused;
        if (isPaused)
            _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _channel = Channel.CreateBounded<PcmChunk>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        _playbackTask = RunAsync(source, track, transcoder, voiceOutput, resolvedSource, logger);
    }

    private async Task RunAsync(IAudioSource source, Track track, IAudioTranscoder transcoder, IVoiceOutput voiceOutput,
        ResolvedAudioSource? resolvedSource, ILogger<NativePlaybackInstance>? logger)
    {
        var ct = _cts.Token;

        try
        {
            var producer = ProduceSourceAsync(source, track, transcoder, resolvedSource, logger, ct);
            var consumer = ConsumeAsync(voiceOutput, ct);

            await Task.WhenAll(producer, consumer);
        }
        finally
        {
            _channel.Writer.TryComplete();

            DrainAndReturnBuffers();
        }
    }

    private async Task ProduceSourceAsync(IAudioSource source, Track track, IAudioTranscoder transcoder,
        ResolvedAudioSource? resolvedSource, ILogger<NativePlaybackInstance>? logger, CancellationToken ct)
    {
        try
        {
            if (resolvedSource is null)
            {
                await using var sourceStream = await source.OpenAsync(track, ct);
                await using var pcmStream = await transcoder.OpenPcmStreamAsync(sourceStream, ct);
                await ProduceAsync(pcmStream, ct);
                return;
            }

            var retryDelay = TimeSpan.FromSeconds(1);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var previousBytes = _bytesProduced;
                try
                {
                    resolvedSource ??= await source.ResolveAsync(track, ct);
                    if (resolvedSource.IsLive)
                        throw new Application.Abstractions.UserFriendlyException("This song doesn't support seeking.");
                    // Retain queued PCM and resume after it, so reconnect does not duplicate buffered audio.
                    var position = _startPosition + TimeSpan.FromSeconds(_bytesProduced / (double)PcmBytesPerSecond);
                    await using var pcmStream = await transcoder.OpenPcmStreamAsync(resolvedSource, position, ct);
                    await ProduceAsync(pcmStream, ct);
                    return;
                }
                catch (AudioProcessException ex) when (ex.IsTransient && !ct.IsCancellationRequested)
                {
                    if (_bytesProduced > previousBytes)
                        retryDelay = TimeSpan.FromSeconds(1);
                    logger?.LogWarning("Failed to read audio for track '{TrackTitle}'. Refreshing the source in {RetrySeconds} seconds.",
                        track.Title, retryDelay.TotalSeconds);
                    resolvedSource = null;
                    await Task.Delay(retryDelay, ct);
                    retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 30));
                }
            }
        }
        catch
        {
            _cts.Cancel();
            throw;
        }
        finally { _channel.Writer.TryComplete(); }
    }

    private async Task ProduceAsync(Stream input, CancellationToken ct)
    {
        var trailingBytes = new byte[sizeof(int)];
        var trailingByteCount = 0;

        while (!ct.IsCancellationRequested)
        {
            byte[]? buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + trailingBytes.Length - 1);

            try
            {
                if (trailingByteCount > 0)
                    trailingBytes.AsSpan(0, trailingByteCount).CopyTo(buffer);

                var read = await input.ReadAsync(buffer.AsMemory(trailingByteCount, ChunkSize), ct);

                if (read == 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                    break;
                }

                var length = trailingByteCount + read;
                var alignedLength = length - length % sizeof(int);
                trailingByteCount = length - alignedLength;
                if (trailingByteCount > 0)
                    buffer.AsSpan(alignedLength, trailingByteCount).CopyTo(trailingBytes);

                if (alignedLength == 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                    continue;
                }

                await _channel.Writer.WriteAsync(new PcmChunk(buffer, alignedLength), ct);
                _bytesProduced += alignedLength;

                buffer = null;
            }
            catch
            {
                if (buffer is not null)
                    ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
        }
    }

    private async Task ConsumeAsync(IVoiceOutput voiceOutput, CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await WaitUntilResumedAsync(ct);
                    ApplyVolume(chunk.Buffer, chunk.Length, Volatile.Read(ref _volume).Value);
                    await voiceOutput.WritePcmAsync(chunk.Buffer.AsMemory(0, chunk.Length), ct);
                    Interlocked.Add(ref _bytesWritten, chunk.Length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk.Buffer);
                }
            }

            await WaitUntilResumedAsync(ct);
            await voiceOutput.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
            _cts.Cancel();
            throw;
        }
    }

    private void DrainAndReturnBuffers()
    {
        while (_channel.Reader.TryRead(out var frame))
        {
            ArrayPool<byte>.Shared.Return(frame.Buffer);
        }
    }

    public Task PauseAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_pauseSync)
        {
            if (_playbackTask.IsCompleted || _isPaused)
                return Task.CompletedTask;

            _isPaused = true;
            _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_pauseSync)
        {
            if (!_isPaused)
                return Task.CompletedTask;

            _isPaused = false;
            _resumeSignal.TrySetResult();
        }

        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(Volume volume, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Volatile.Write(ref _volume, volume);
        return Task.CompletedTask;
    }

    private async Task StopAsync()
    {
        _cts.Cancel();

        try
        {
            await _playbackTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(); }
        finally { _cts.Dispose(); }
    }

    private Task WaitUntilResumedAsync(CancellationToken ct)
    {
        Task resumeTask;
        lock (_pauseSync)
            resumeTask = _isPaused ? _resumeSignal.Task : Task.CompletedTask;

        return resumeTask.WaitAsync(ct);
    }

    private static TaskCompletionSource CreateSignaledSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    private static void ApplyVolume(byte[] pcm, int length, int volume)
    {
        if (volume == 100)
            return;

        var gain = volume / 100f;
        for (var offset = 0; offset + sizeof(short) <= length; offset += sizeof(short))
        {
            var sample = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(offset, sizeof(short)));
            var scaled = (int)MathF.Round(sample * gain);
            var clipped = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, sizeof(short)), clipped);
        }
    }
}
