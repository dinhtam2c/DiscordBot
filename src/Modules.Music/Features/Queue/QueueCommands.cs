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
    [SlashCommand("queue", "Show the current track and the tracks waiting in the queue.")]
    public async Task QueueAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("No player is active. Add a track with `/play` first.");
        if (!player.HasCurrentTrack && !player.HasQueue)
        {
            await CompleteAsync("The queue is empty.");
            return;
        }

        var description = new StringBuilder();
        if (player.CurrentTrack is { } current)
        {
            description.AppendLine($"**{(player.IsPaused ? "Paused" : "Now playing")}:** {Format.Url(Format.Sanitize(current.Title), current.SourceUrl)}");
            description.AppendLine();
        }
        if (player.HasQueue)
        {
            description.AppendLine("**Up next:**");
            for (var index = 0; index < player.Queue.Count; index++)
            {
                var track = player.Queue[index].Track;
                description.AppendLine($"{index + 1}. {Format.Url(Format.Sanitize(track.Title), track.SourceUrl)} — "
                    + (track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss")));
            }
        }
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle("Queue")
            .WithDescription(description.ToString().TrimEnd())
            .Build());
    }

    [SlashCommand("clear", "Remove all waiting tracks from the queue without stopping the current track.")]
    public async Task ClearAsync()
    {
        await players.ExecuteAsync(GuildId, new ClearQueue(), Ct);
        await CompleteAsync("Removed all waiting tracks from the queue.");
    }

    [SlashCommand("shuffle", "Shuffle the waiting tracks without changing the current track.")]
    public async Task ShuffleAsync()
    {
        await players.ExecuteAsync(GuildId, new ShuffleQueue(), Ct);
        await CompleteAsync("Shuffled the waiting tracks.");
    }

    [SlashCommand("remove", "Remove a waiting track by its position in the queue.")]
    public async Task RemoveAsync([Summary(description: "Queue position of the track to remove, starting at 1.")] long index)
    {
        await players.ExecuteAsync(GuildId, new RemoveTrack(index), Ct);
        await CompleteAsync($"Removed the track at position **#{index}** from the queue.");
    }

    [SlashCommand("move", "Move a waiting track to another position in the queue.")]
    public async Task MoveAsync(
        [Summary(description: "Current queue position of the track, starting at 1.")] long index,
        [Summary("new_index", "Destination queue position, starting at 1.")] long newIndex)
    {
        await players.ExecuteAsync(GuildId, new MoveTrack(index, newIndex), Ct);
        await CompleteAsync($"Moved the track from **#{index}** to **#{newIndex}** in the queue.");
    }
}
