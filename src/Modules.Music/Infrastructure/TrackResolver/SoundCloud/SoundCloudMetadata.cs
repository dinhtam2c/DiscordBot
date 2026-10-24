using System.Text.Json.Serialization;

namespace Modules.Music.Infrastructure.TrackResolver.SoundCloud;

internal sealed class SoundCloudMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = null!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("artist")]
    public string? Artist { get; init; }

    [JsonPropertyName("artists")]
    public List<string>? Artists { get; init; }

    [JsonPropertyName("uploader")]
    public string? Uploader { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("duration")]
    public double? Duration { get; init; }

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; init; }

    [JsonPropertyName("original_url")]
    public string? OriginalUrl { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("entries")]
    public List<SoundCloudMetadata>? Entries { get; init; }

    [JsonPropertyName("thumbnails")]
    public List<SoundCloudThumbnail>? Thumbnails { get; init; }
}

internal sealed class SoundCloudThumbnail
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }
}
