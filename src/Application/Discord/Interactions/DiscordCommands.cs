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

    // Slash commands defer a reply; components defer an update to their source message.
    protected Task CompleteAsync(string? text = null, Embed? embed = null, MessageComponent? components = null)
        => Context.Interaction.ModifyOriginalResponseAsync(message =>
        {
            message.Content = text ?? string.Empty;
            message.Embeds = embed is null ? Array.Empty<Embed>() : [embed];
            message.Components = components ?? new ComponentBuilder().Build();
            message.AllowedMentions = AllowedMentions.None;
        }, new RequestOptions { CancelToken = Ct });
}
