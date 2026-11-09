using System.Text;
using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Queue;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class QueueCommands(PlayerCoordinator players, IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("queue", "See what's playing and what's coming up next.")]
    public async Task QueueAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("Use `/play` to start playing music first.");
        if (!player.HasCurrentTrack && !player.HasQueue)
        {
            await CompleteAsync("The queue is empty.");
            return;
        }

        var description = new StringBuilder();
        if (player.CurrentTrack is { } current)
        {
            description.AppendLine($"**{(player.IsPaused ? "Paused" : "Now playing")}:** {TrackFormatting.Entry(current)}");
            description.AppendLine();
        }
        if (player.HasQueue)
        {
            description.AppendLine("**Up next:**");
            for (var index = 0; index < player.Queue.Count; index++)
            {
                description.AppendLine($"{index + 1}. {TrackFormatting.Entry(player.Queue[index].Track)}");
            }
        }
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle("Queue")
            .WithDescription(description.ToString().TrimEnd())
            .Build());
    }

    [SlashCommand("clear", "Clear the queue without stopping the current song.")]
    public async Task ClearAsync()
    {
        await players.ExecuteAsync(GuildId, new ClearQueue(), Ct);
        await CompleteAsync("Cleared the queue.");
    }

    [SlashCommand("shuffle", "Shuffle the songs in the queue.")]
    public async Task ShuffleAsync()
    {
        await players.ExecuteAsync(GuildId, new ShuffleQueue(), Ct);
        await CompleteAsync("Shuffled the queue.");
    }

    [SlashCommand("remove", "Remove a song from the queue.")]
    public async Task RemoveAsync([Summary(description: "The song's number in `/queue`, starting at 1.")] long index)
    {
        await players.ExecuteAsync(GuildId, new RemoveTrack(index), Ct);
        await CompleteAsync($"Removed song **#{index}** from the queue.");
    }

    [SlashCommand("move", "Move a song to a different spot in the queue.")]
    public async Task MoveAsync(
        [Summary(description: "The song's current number in `/queue`, starting at 1.")] long index,
        [Summary("new_index", "Where to move the song in the queue, starting at 1.")] long newIndex)
    {
        await players.ExecuteAsync(GuildId, new MoveTrack(index, newIndex), Ct);
        await CompleteAsync($"Moved song **#{index}** to **#{newIndex}** in the queue.");
    }
}
