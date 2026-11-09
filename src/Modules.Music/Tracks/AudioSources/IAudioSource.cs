namespace Modules.Music.Tracks.AudioSources;

internal interface IAudioSource
{
    TrackSource Source { get; }

    Task<Stream> OpenAsync(Track track, CancellationToken ct);

    Task<string> GetUrlAsync(Track track, CancellationToken ct);
}
