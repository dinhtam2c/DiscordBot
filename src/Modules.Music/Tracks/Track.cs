namespace Modules.Music.Tracks;

internal sealed record Track(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> Artists,
    TimeSpan Duration,
    TrackSource Source,
    string SourceUrl,
    string ThumbnailUrl,
    string? UploaderName = null,
    string? UploaderUrl = null)
{
    public string Title { get; init; } = string.IsNullOrWhiteSpace(Title) ? "Unknown song" : Title;

    public static IReadOnlyList<string> SelectArtists(params IEnumerable<string?>?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var names = candidate?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names is { Length: > 0 })
                return Array.AsReadOnly(names);
        }

        return Array.Empty<string>();
    }
}
