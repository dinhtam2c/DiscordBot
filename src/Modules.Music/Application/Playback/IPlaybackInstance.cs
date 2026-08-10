using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal interface IPlaybackInstance : IAsyncDisposable
{
    bool IsPlaying { get; }

    bool IsPaused { get; }

    Task Completion { get; }

    Task PauseAsync(CancellationToken ct);

    Task ResumeAsync(CancellationToken ct);

    Task SeekAsync(TimeSpan position, CancellationToken ct);

    Task SetVolumeAsync(Volume volume, CancellationToken ct);
}
