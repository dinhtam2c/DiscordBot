namespace Application.Discord.Interactions.Response;

public sealed class InteractionResponse
{
    public string? Content { get; init; }

    public bool Ephemeral { get; init; }

    public IReadOnlyList<ResponseEmbed> Embeds { get; init; } = [];

    public IReadOnlyList<ResponseActionRow> Rows { get; init; } = [];

    public static InteractionResponse Text(
        string content,
        bool ephemeral = false)
    {
        return new()
        {
            Content = content,
            Ephemeral = ephemeral
        };
    }

    public static InteractionResponse Create(
        string? content = null,
        IReadOnlyList<ResponseEmbed>? embeds = null,
        IReadOnlyList<ResponseActionRow>? rows = null,
        bool ephemeral = false)
    {
        return new()
        {
            Content = content,
            Embeds = embeds ?? [],
            Rows = rows ?? [],
            Ephemeral = ephemeral
        };
    }
}
