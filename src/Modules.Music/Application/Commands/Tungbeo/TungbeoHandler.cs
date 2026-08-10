using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.User;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Commands.Play;
using Modules.Music.Application.Repositories;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Commands.Tungbeo;

[InteractionHandler("tungbeo")]
internal sealed class TungbeoHandler(
    IPlayerRepository playerRepository,
    IDiscordUserService discordUserService,
    ITrackResolverFactory resolverFactory,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PlayHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await playerRepository.GetByIdAsync(guildId, ct);
        if (player is null)
        {
            player = Player.Create(guildId);
            await playerRepository.AddAsync(player, ct);
        }

        if (player.VoiceChannelId is null)
        {
            var voiceChannelInfo = await discordUserService.GetUserVoiceChannelAsync(guildId, context.UserId, ct)
                ?? throw new UserFriendlyException("You are not in a voice channel.");

            player.Join(voiceChannelInfo.Id);
        }

        var query = "https://www.youtube.com/watch?v=o0g5cBuUyLE";
        var trackResolver = resolverFactory.Select(query);

        var tracks = await trackResolver.ResolveAsync(query, ct)
            ?? throw new UserFriendlyException($"No track found for query: {query}");

        logger.LogInformation("Resolved {TrackCount} tracks for query: {Query}", tracks.Count, query);

        if (!tracks.Any())
        {
            throw new UserFriendlyException($"No track found for query: {query}");
        }

        var track = tracks[0];
        var queueItem = new QueueItem(track, context.UserId, clock.UtcNow);

        var now = !player.HasCurrentTrack;
        player.PlayNext(queueItem);

        await context.Responder.RespondAsync(
            new InteractionResponse
            {
                Embeds =
                [
                    new ResponseEmbed
                    {
                        Title = "Added to queue",
                        Description = $"**[{track.Title}]({track.SourceUrl})**",
                        ThumbnailUrl = track.ThumbnailUrl,
                        Fields =
                        [
                            new("Artist", track.Artist, true),
                            new("Duration", track.Duration.ToString(@"mm\:ss"), true),
                            new("Position", now ? "Now Playing" : "1", true),
                        ]
                    }
                ]
            },
            ct);

        await unitOfWork.CommitAsync(ct);
    }
}
