namespace Application.Discord.Interactions.Response;

public interface IInteractionResponder
{
    Task DeferAsync(CancellationToken ct, bool ephemeral = false);

    Task RespondAsync(InteractionResponse content, CancellationToken ct);
}
