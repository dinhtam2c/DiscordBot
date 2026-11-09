using Application.Abstractions;
using Discord;
using Discord.Interactions;
using Domain.ValueObjects;

namespace Application.Discord.Interactions;

public abstract class DiscordCommands(IAppLifetime lifetime) : InteractionModuleBase<SocketInteractionContext>
{
    protected CancellationToken Ct => lifetime.Stopping;

    protected GuildId GuildId => Context.Guild is { } guild
        ? new GuildId(guild.Id)
        : throw new UserFriendlyException("Use this command in a server, not in a DM.");

    protected UserId UserId => new(Context.User.Id);

    // Slash commands are deferred by the gateway adapter before execution.
    protected Task CompleteAsync(string? text = null, Embed? embed = null)
        => Context.Interaction.ModifyOriginalResponseAsync(message =>
        {
            message.Content = text ?? string.Empty;
            message.Embeds = embed is null ? Array.Empty<Embed>() : [embed];
        }, new RequestOptions { CancelToken = Ct });
}
