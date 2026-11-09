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
    [SlashCommand("join", "Join your voice channel or move there without interrupting playback.")]
    public async Task JoinAsync()
    {
        var channel = (Context.User as IGuildUser)?.VoiceChannel
            ?? throw new UserFriendlyException("Join a voice channel first.");
        await players.ExecuteAsync(GuildId, new JoinVoice(new ChannelId(channel.Id)), Ct);
        await CompleteAsync($"Joining <#{channel.Id}>.");
    }

    [SlashCommand("leave", "Leave the voice channel, stop playback, and clear the queue.")]
    public async Task LeaveAsync()
    {
        await players.ExecuteAsync(GuildId, new LeaveVoice(), Ct);
        await CompleteAsync("Leaving the voice channel. Playback stopped and queue cleared.");
    }
}
