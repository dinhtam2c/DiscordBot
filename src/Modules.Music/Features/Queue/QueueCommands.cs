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
    [SlashCommand("queue", "Show the current playback queue.")]
    public async Task QueueAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");
        if (!player.HasCurrentTrack && !player.HasQueue)
        {
            await CompleteAsync("The queue is empty.");
            return;
        }

        var description = new StringBuilder();
        if (player.CurrentTrack is { } current)
        {
            description.AppendLine($"**Now playing:** [{current.Title}]({current.SourceUrl})");
            description.AppendLine();
        }
        if (player.HasQueue)
        {
            description.AppendLine("**Up next:**");
            for (var index = 0; index < player.Queue.Count; index++)
            {
                var track = player.Queue[index].Track;
                description.AppendLine($"{index + 1}. [{track.Title}]({track.SourceUrl}) — "
                    + (track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss")));
            }
        }
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle("Queue")
            .WithDescription(description.ToString().TrimEnd())
            .Build());
    }

    [SlashCommand("clear", "Clear the current playback queue.")]
    public async Task ClearAsync()
    {
        await players.ExecuteAsync(GuildId, new ClearQueue(), Ct);
        await CompleteAsync("Queue cleared.");
    }

    [SlashCommand("shuffle", "Shuffle the current playback queue.")]
    public async Task ShuffleAsync()
    {
        await players.ExecuteAsync(GuildId, new ShuffleQueue(), Ct);
        await CompleteAsync("Queue shuffled.");
    }

    [SlashCommand("remove", "Remove a track from the queue.")]
    public async Task RemoveAsync([Summary(description: "The index of the track to remove.")] long index)
    {
        await players.ExecuteAsync(GuildId, new RemoveTrack(index), Ct);
        await CompleteAsync($"Removed track at position {index} from the queue.");
    }

    [SlashCommand("move", "Move a track in the queue.")]
    public async Task MoveAsync(
        [Summary(description: "The index of the track to move.")] long index,
        [Summary("new_index", "The new index of the track.")] long newIndex)
    {
        await players.ExecuteAsync(GuildId, new MoveTrack(index, newIndex), Ct);
        await CompleteAsync($"Moved track from position {index} to {newIndex} in the queue.");
    }
}
