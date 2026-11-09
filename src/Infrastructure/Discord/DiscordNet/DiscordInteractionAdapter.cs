using System.Diagnostics;
using System.Reflection;
using Application.Abstractions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordInteractionAdapter(
    DiscordSocketClient client,
    InteractionService commands,
    IServiceScopeFactory scopes,
    IAppLifetime lifetime,
    ILogger<DiscordInteractionAdapter> logger)
{
    public Task HandleAsync(SocketInteraction interaction)
    {
        if (lifetime.Stopping.IsCancellationRequested)
            return Task.CompletedTask;

        // Await the complete command in its scope, without holding the gateway event loop.
        _ = ExecuteAsync(interaction);
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(SocketInteraction interaction)
    {
        var ct = lifetime.Stopping;
        var stopwatch = Stopwatch.StartNew();
        var commandName = interaction switch
        {
            SocketSlashCommand slash => slash.CommandName,
            SocketMessageComponent component => component.Data.CustomId,
            _ => interaction.Type.ToString(),
        };
        logger.LogDebug("Received interaction '{CommandName}'.", commandName);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (interaction is SocketSlashCommand)
                await interaction.DeferAsync(options: new RequestOptions { CancelToken = ct });
            else if (interaction is SocketMessageComponent component)
                await component.DeferAsync(options: new RequestOptions { CancelToken = ct });

            await using var scope = scopes.CreateAsyncScope();
            var result = await commands.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), scope.ServiceProvider);
            if (!result.IsSuccess)
            {
                if (result is ExecuteResult { Exception: { } exception })
                    throw exception;
                logger.LogWarning("Interaction '{CommandName}' failed: {Reason}", commandName, result.ErrorReason);
                await RespondWithErrorAsync(interaction, result.Error == InteractionCommandError.UnknownCommand
                    ? "This command is no longer available. Use `/help` to see what you can do."
                    : result.ErrorReason, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Interaction '{CommandName}' was canceled.", commandName);
        }
        catch (Exception ex)
        {
            while (ex is TargetInvocationException or InteractionException && ex.InnerException is not null)
                ex = ex.InnerException;
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
                return;

            var message = "Something went wrong. Please try again.";
            if (ex is UserFriendlyException)
            {
                message = ex.Message;
                logger.LogWarning("Interaction '{CommandName}' failed: {Reason}", commandName, message);
            }
            else
            {
                logger.LogError(ex, "Failed to handle interaction '{CommandName}'.", commandName);
            }
            await RespondWithErrorAsync(interaction, message, ct);
        }
        finally
        {
            logger.LogDebug("Finished handling interaction '{CommandName}' in {ElapsedMilliseconds} ms.",
                commandName, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task RespondWithErrorAsync(SocketInteraction interaction, string message, CancellationToken ct)
    {
        try
        {
            if (interaction is SocketAutocompleteInteraction)
                return;
            var options = new RequestOptions { CancelToken = ct };
            if (interaction is SocketMessageComponent && interaction.HasResponded)
            {
                // A deferred component points at the shared message. Errors belong to the clicking user.
                await interaction.FollowupAsync(message, ephemeral: true, allowedMentions: AllowedMentions.None, options: options);
            }
            else if (interaction.HasResponded)
            {
                await interaction.ModifyOriginalResponseAsync(response =>
                {
                    response.Content = message;
                    response.Embeds = Array.Empty<Embed>();
                    response.Components = new ComponentBuilder().Build();
                }, options);
            }
            else
            {
                await interaction.RespondAsync(message, ephemeral: true, options: options);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send the interaction error response.");
        }
    }
}
