namespace Application.Discord.Interactions.Response;

public sealed class ResponseButton : ResponseComponent
{
    public required string CustomId { get; init; }

    public required string Label { get; init; }

    public ButtonStyle Style { get; init; } = ButtonStyle.Primary;

    public string? Emoji { get; init; }

    public bool Disabled { get; init; }
}
