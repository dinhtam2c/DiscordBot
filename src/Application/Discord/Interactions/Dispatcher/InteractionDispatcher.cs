using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Application.Discord.Interactions.Dispatcher;

public sealed class InteractionDispatcher(
    InteractionHandlerResolver resolver,
    IServiceProvider serviceProvider,
    ILogger<InteractionDispatcher> logger)
{
    public async Task DispatchAsync(InteractionContext context, CancellationToken ct)
    {
        logger.LogDebug("Received interaction for command: /{CommandName}", context.CommandName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await context.Responder.DeferAsync(ct);

            if (!resolver.TryResolve(context.CommandName, out var handlerType))
            {
                logger.LogWarning("No handler found for command: /{CommandName}", context.CommandName);
                await context.Responder.RespondAsync(
                    InteractionResponse.Text($"The command /{context.CommandName} is no longer available."),
                    ct);
                return;
            }

            var handler = (IInteractionHandler)serviceProvider.GetRequiredService(handlerType!);
            await handler.HandleAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Interaction cancelled for command: /{CommandName}", context.CommandName);
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
        finally
        {
            stopwatch.Stop();
            logger.LogDebug(
                "Handled interaction for command: /{CommandName} in {ElapsedMilliseconds} ms",
                context.CommandName,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
