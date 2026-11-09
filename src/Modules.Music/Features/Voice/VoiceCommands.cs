using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Domain.ValueObjects;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Voice;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class VoiceCommands(PlayerCoordinator players, IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("join", "Join your voice channel.")]
    public async Task JoinAsync()
    {
        var channel = (Context.User as IGuildUser)?.VoiceChannel
            ?? throw new UserFriendlyException("You are not in a voice channel.");
        await players.ExecuteAsync(GuildId, new JoinVoice(new ChannelId(channel.Id)), Ct);
        await CompleteAsync($"Joining **{channel.Name}**");
    }

    [SlashCommand("leave", "Leave the voice channel.")]
    public async Task LeaveAsync()
    {
        await players.ExecuteAsync(GuildId, new LeaveVoice(), Ct);
        await CompleteAsync("Left.");
    }
}
