using Modules.Music.Domain.Players;

namespace Modules.Music.Application.AudioSource;

internal interface IAudioSource
{
    TrackSource Source { get; }

    Task<Stream> OpenAsync(Track track, CancellationToken ct);
}
