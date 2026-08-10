using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.User;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players;
using System.Globalization;

namespace Modules.Music.Application.Commands.NowPlaying;

[InteractionHandler("nowplaying")]
internal sealed class NowPlayingHandler(
    IPlayerRepository playerRepository,
    IDiscordUserService discordUserService) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("No player found for this server.");

        if (!player.HasCurrentTrack)
        {
            await context.Responder.RespondAsync(InteractionResponse.Text("Nothing is currently playing."), ct);
            return;
        }

        var currentQueueItem = player.CurrentQueueItem!;
        var currentTrack = currentQueueItem.Track;
        var requester = await discordUserService.GetUserAsync(guildId, currentQueueItem.RequesterId, ct);
        var requesterName = requester?.DisplayName ?? requester?.Username ?? $"User {currentQueueItem.RequesterId}";

        await context.Responder.RespondAsync(
            new InteractionResponse
            {
                Embeds =
                [
                    new ResponseEmbed
                    {
                        Title = "Now Playing",
                        Description = $"**[{currentTrack.Title}]({currentTrack.SourceUrl})**",
                        ThumbnailUrl = currentTrack.ThumbnailUrl,
                        Fields =
                        [
                            new("Artist", currentTrack.Artist, true),
                            new("Duration", currentTrack.Duration.TotalHours >= 1 ? currentTrack.Duration.ToString(@"hh\:mm\:ss") : currentTrack.Duration.ToString(@"mm\:ss"), true),
                            new("Requested by", requesterName, true),
                            new("Requested at", currentQueueItem.RequestedAt.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture), true),
                        ]
                    }
                ]
            },
            ct);
    }
}
