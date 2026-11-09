using Modules.Music.Players;

namespace Modules.Music.Playback;

internal interface IPlaybackInstance : IAsyncDisposable
{
    Task Completion { get; }

    // Audio accepted by the output, which may be slightly ahead of what listeners hear.
    TimeSpan Position { get; }

    Task PauseAsync(CancellationToken ct);

    Task ResumeAsync(CancellationToken ct);

    Task SetVolumeAsync(Volume volume, CancellationToken ct);
}
