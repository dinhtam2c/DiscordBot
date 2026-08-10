using Application.Discord.Interactions.Context;

namespace Application.Discord.Interactions.Handler;

public interface IInteractionHandler
{
    Task HandleAsync(InteractionContext context, CancellationToken ct);
}
