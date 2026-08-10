namespace Application.Discord.Interactions.Response;

public sealed class ResponseActionRow
{
    public IReadOnlyList<ResponseComponent> Components { get; init; } = [];
}