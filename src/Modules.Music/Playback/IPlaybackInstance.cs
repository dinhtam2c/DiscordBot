using Modules.Music.Players;

namespace Modules.Music.Playback;

internal interface IPlaybackInstance : IAsyncDisposable
{
    Task Completion { get; }

    Task PauseAsync(CancellationToken ct);

    Task ResumeAsync(CancellationToken ct);

    Task SetVolumeAsync(Volume volume, CancellationToken ct);
}
