using Application.Discord.Voice;
using Modules.Music.Application.AudioSource;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal sealed class NativePlaybackInstance : IPlaybackInstance, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;

    private readonly Task _playbackTask;

    private const int ChunkSize = 3840 * 10; // 200 ms
    private const int ChannelCapacity = 10;  // ~2 seconds

    private readonly record struct PcmChunk(byte[] Buffer, int Length);

    private readonly Channel<PcmChunk> _channel;

    public Task Completion => _playbackTask;

    public bool IsPlaying => !_playbackTask.IsCompleted;

    public bool IsPaused => false;

    public NativePlaybackInstance(
        IAudioSource source,
        Track track,
        IAudioTranscoder transcoder,
        IVoiceSession voiceSession,
        CancellationToken externalToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _channel = Channel.CreateBounded<PcmChunk>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        _playbackTask = RunAsync(source, track, transcoder, voiceSession);
    }

    private async Task RunAsync(IAudioSource source, Track track, IAudioTranscoder transcoder, IVoiceSession voiceSession)
    {
        var ct = _cts.Token;

        try
        {
            var url = await source.GetUrlAsync(track, ct);

            await using var pcmStream = await transcoder.OpenPcmStreamAsync(url, ct);

            var producer = ProduceAsync(pcmStream, ct);
            var consumer = ConsumeAsync(voiceSession, ct);

            await Task.WhenAll(producer, consumer);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {

        }
        finally
        {
            _channel.Writer.TryComplete();

            DrainAndReturnBuffers();
        }
    }

    private async Task ProduceAsync(Stream input, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

                try
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, ChunkSize), ct);

                    if (read == 0)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        break;
                    }

                    await _channel.Writer.WriteAsync(new PcmChunk(buffer, read), ct);

                    buffer = null!;
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    throw;
                }
            }
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    private async Task ConsumeAsync(IVoiceSession voiceSession, CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await voiceSession.WritePcmAsync(chunk.Buffer.AsMemory(0, chunk.Length), ct);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk.Buffer);
                }
            }

            await voiceSession.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
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
        throw new NotImplementedException();
    }

    public Task ResumeAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SeekAsync(TimeSpan position, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SetVolumeAsync(Volume volume, CancellationToken ct)
    {
        throw new NotImplementedException();
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
        await StopAsync();
        _cts.Dispose();
    }
}