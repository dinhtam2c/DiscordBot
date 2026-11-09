namespace Modules.Music.Tracks.Resolution;

internal sealed class TrackSearchOptions
{
    public const string SectionName = "Music:TrackSearch";

    public int SearchResultCount { get; init; } = 5;
}
