using Application.Abstractions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Dispatcher;
using Discord.WebSocket;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordInteractionAdapter(
    DiscordSocketClient client,
    IServiceScopeFactory scopeFactory,
    IAppLifetime appLifetime) : IDiscordEventSubscriber
{
    public void Subscribe()
    {
        client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var responder = new DiscordInteractionResponder(command);

        var options = new InteractionOptions(command.Data.Options.ToDictionary(option => option.Name, option => MapOption(option.Value)));

        var context = new InteractionContext(
            command.CommandName,
            new UserId(command.User.Id),
            command.ChannelId.HasValue ? new ChannelId(command.ChannelId.Value) : null,
            command.GuildId.HasValue ? new GuildId(command.GuildId.Value) : null,
            options,
            responder);

        _ = RunDetachedAsync(context);
    }

    private async Task RunDetachedAsync(InteractionContext context)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<InteractionDispatcher>();
            await dispatcher.DispatchAsync(context, appLifetime.Stopping);
        }
        catch
        {
            // Dispatcher should normally handle command exceptions itself.
        }
    }

    private static object MapOption(object value)
    {
        return value switch
        {
            SocketGuildUser user => new UserId(user.Id),

            SocketUser user => new UserId(user.Id),

            SocketRole role => new RoleId(role.Id),

            SocketGuildChannel channel => new ChannelId(channel.Id),

            _ => value
        };
    }
}
