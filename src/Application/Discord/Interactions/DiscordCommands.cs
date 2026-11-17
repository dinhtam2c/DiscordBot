using Application.Abstractions;
using Application.Discord.Messages;
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
    protected Task CompleteAsync(string? text = null, MessageComponent? components = null)
        => Context.Interaction.ModifyOriginalResponseAsync(message =>
            DiscordMessageContent.Apply(message, text, components), new RequestOptions { CancelToken = Ct });

    protected Task FollowupV2Async(string? text = null, MessageComponent? components = null, bool ephemeral = false)
        => FollowupAsync(components: DiscordMessageContent.Create(text, components), ephemeral: ephemeral,
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None,
            options: new RequestOptions { CancelToken = Ct });
}
