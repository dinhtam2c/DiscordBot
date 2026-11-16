using System.Text.Json.Serialization;

namespace Modules.Music.Tracks.Youtube;

internal sealed class YoutubeMetadata
{
    [JsonPropertyName("is_live")]
    public bool? IsLive { get; init; }

    [JsonPropertyName("availability")]
    public string? Availability { get; init; }

    [JsonPropertyName("_type")]
    public string? Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("artists")]
    public List<string>? Artists { get; init; }

    [JsonPropertyName("artist")]
    public string? Artist { get; init; }

    [JsonPropertyName("creators")]
    public List<string>? Creators { get; init; }

    [JsonPropertyName("creator")]
    public string? Creator { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("channel_url")]
    public string? ChannelUrl { get; init; }

    [JsonPropertyName("uploader")]
    public string? Uploader { get; init; }

    [JsonPropertyName("uploader_url")]
    public string? UploaderUrl { get; init; }

    [JsonPropertyName("duration")]
    public double? Duration { get; init; }

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("thumbnails")]
    public List<YoutubeThumbnail>? Thumbnails { get; init; }

    [JsonPropertyName("entries")]
    public List<YoutubeMetadata>? Entries { get; init; }
}

internal sealed class YoutubeThumbnail
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }
}
