namespace Application.Discord.Interactions.Response;

public sealed class ResponseSelectMenu : ResponseComponent
{
    public required string CustomId { get; init; }

    public string? Placeholder { get; init; }

    public int MinValues { get; init; } = 1;

    public int MaxValues { get; init; } = 1;

    public bool Disabled { get; init; }

    public IReadOnlyList<ResponseSelectOption> Options { get; init; } = [];
}