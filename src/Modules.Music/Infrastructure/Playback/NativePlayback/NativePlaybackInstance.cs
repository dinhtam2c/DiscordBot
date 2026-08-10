using Application.Discord.Voice;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.AudioSource;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;
using System.Buffers;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal sealed class NativePlaybackInstance : IPlaybackInstance
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Task _playbackTask;

    // 200 ms PCM @ 48 kHz, stereo, s16le
    // 3840 bytes = 20 ms
    private const int BufferSize = 3840;

    public Task Completion => _playbackTask;

    public bool IsPlaying => !_playbackTask.IsCompleted;

    public bool IsPaused => false;

    public NativePlaybackInstance(IAudioSource source, Track track, IAudioTranscoder encoder, IVoiceSession voiceSession)
    {
        _playbackTask = RunAsync(source, track, encoder, voiceSession);
    }

    private async Task RunAsync(IAudioSource source, Track track, IAudioTranscoder encoder, IVoiceSession voiceSession)
    {
        var ct = _cts.Token;

        await using var sourceStream = await source.OpenAsync(track, ct);
        await using var pcmStream = await encoder.TranscodePcmAsync(sourceStream, ct);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await pcmStream.ReadAsync(buffer.AsMemory(0, BufferSize), ct);

                if (read == 0)
                    break;

                await voiceSession.WritePcmAsync(buffer.AsMemory(0, read), ct);
            }

            await voiceSession.FlushAsync(ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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

    public async Task StopAsync(CancellationToken ct)
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
        await StopAsync(CancellationToken.None);
        _cts.Dispose();
    }
}