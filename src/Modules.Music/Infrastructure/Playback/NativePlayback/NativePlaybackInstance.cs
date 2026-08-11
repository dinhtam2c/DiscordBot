using Application.Discord.Voice;
using Modules.Music.Application.AudioSource;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;
using System.Buffers;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal sealed class NativePlaybackInstance : IPlaybackInstance, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;

    private readonly Task _playbackTask;

    private const int BufferSize = 3840 * 10;

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
        _playbackTask = RunAsync(source, track, transcoder, voiceSession);
    }

    private async Task RunAsync(IAudioSource source, Track track, IAudioTranscoder transcoder, IVoiceSession voiceSession)
    {
        var ct = _cts.Token;

        var url = await source.GetUrlAsync(track, ct);
        await using var pcmStream = await transcoder.OpenPcmStreamAsync(url, ct);

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