namespace Application.Discord.Interactions.Response;

public sealed class ResponseSelectOption
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public string? Description { get; init; }

    public string? Emoji { get; init; }

    public bool Default { get; init; }
}