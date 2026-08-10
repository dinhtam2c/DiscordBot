using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Application.Discord.Interactions.Dispatcher;

public sealed class InteractionDispatcher(
    InteractionHandlerResolver resolver,
    IServiceScopeFactory scopeFactory,
    ILogger<InteractionDispatcher> logger)
{
    public async Task DispatchAsync(InteractionContext context, CancellationToken ct)
    {
        logger.LogDebug("Received interaction for command: /{CommandName}", context.CommandName);

        var handlerType = resolver.Resolve(context.CommandName);

        if (handlerType is null)
        {
            logger.LogWarning("No handler found for command: /{CommandName}", context.CommandName);
            await context.Responder.RespondAsync(
                InteractionResponse.Text($"Unknown command: /{context.CommandName}"),
                ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var handler = (IInteractionHandler)scope.ServiceProvider.GetRequiredService(handlerType);

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await context.Responder.DeferAsync(ct);
            await handler.HandleAsync(context, ct);
        }
        catch (UserFriendlyException ex)
        {
            logger.LogWarning(
                ex,
                "User-friendly error occurred while handling interaction for command: /{CommandName}",
                context.CommandName);


            await context.Responder.RespondAsync(InteractionResponse.Text(ex.Message), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while handling interaction for command: /{CommandName}", context.CommandName);

            await context.Responder.RespondAsync(InteractionResponse.Text("An unexpected error occurred."), ct);
        }

        stopwatch.Stop();
        logger.LogDebug(
            "Handled interaction for command: /{CommandName} in {ElapsedMilliseconds} ms",
            context.CommandName,
            stopwatch.ElapsedMilliseconds);
    }
}
