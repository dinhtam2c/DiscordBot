namespace Application.Discord.Interactions.Response;

public sealed class ResponseEmbed
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Url { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? ImageUrl { get; init; }

    public ResponseFooter? Footer { get; init; }

    public IReadOnlyList<ResponseField> Fields { get; init; } = [];
}
