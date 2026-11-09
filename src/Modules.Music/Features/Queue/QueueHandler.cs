using System.Text;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Queue;

[InteractionHandler("queue")]
internal sealed class QueueHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await players.GetSnapshotAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        if (!player.HasCurrentTrack && !player.HasQueue)
        {
            await context.Responder.RespondAsync(InteractionResponse.Text("The queue is empty."), ct);
            return;
        }

        var description = new StringBuilder();

        if (player.HasCurrentTrack)
        {
            var currentTrack = player.CurrentTrack!;
            description.AppendLine($"**Now playing:** [{currentTrack.Title}]({currentTrack.SourceUrl})");
            description.AppendLine();
        }

        if (player.HasQueue)
        {
            description.AppendLine("**Up next:**");

            for (var index = 0; index < player.Queue.Count; index++)
            {
                var item = player.Queue[index];
                var position = index + 1;
                description.AppendLine($"{position}. [{item.Track.Title}]({item.Track.SourceUrl}) — "
                    + (item.Track.Duration.TotalHours >= 1 ? item.Track.Duration.ToString(@"hh\:mm\:ss") : item.Track.Duration.ToString(@"mm\:ss")));
            }
        }

        await context.Responder.RespondAsync(
            new InteractionResponse
            {
                Embeds =
                [
                    new ResponseEmbed
                    {
                        Title = "Queue",
                        Description = description.ToString().TrimEnd()
                    }
                ]
            },
            ct);
    }
}
