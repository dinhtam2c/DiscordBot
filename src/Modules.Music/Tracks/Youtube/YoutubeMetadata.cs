using System.Text.Json.Serialization;

namespace Modules.Music.Tracks.Youtube;

internal sealed class YoutubeMetadata
{
    [JsonPropertyName("_type")]
    public string? Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = null!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("channel_url")]
    public string? ChannelUrl { get; init; }

    [JsonPropertyName("uploader")]
    public string? Uploader { get; init; }

    [JsonPropertyName("uploader_url")]
    public string? UploaderUrl { get; init; }

    [JsonPropertyName("duration")]
    public int? Duration { get; init; }

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("entries")]
    public List<YoutubeMetadata>? Entries { get; init; }
}
