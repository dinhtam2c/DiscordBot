namespace Application.Discord.Interactions.Response;

public sealed class ResponseFooter
{
    public required string Text { get; init; }

    public string? IconUrl { get; init; }
}
