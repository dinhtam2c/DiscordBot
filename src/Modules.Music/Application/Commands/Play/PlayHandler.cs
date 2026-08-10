using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.User;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Commands.Play;

[InteractionHandler("play")]
internal class PlayHandler(
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

        var query = context.Options.GetRequired<string>("query");
        var trackResolver = resolverFactory.Select(query);

        var tracks = await trackResolver.ResolveAsync(query, ct)
            ?? throw new UserFriendlyException($"No track found for query: {query}");

        logger.LogInformation("Resolved {TrackCount} tracks for query: {Query}", tracks.Count, query);

        if (!tracks.Any())
        {
            throw new UserFriendlyException($"No track found for query: {query}");
        }

        foreach (var track in tracks)
        {
            var queueItem = new QueueItem(track, context.UserId, clock.UtcNow);
            player.Play(queueItem);
        }

        if (tracks.Count == 1)
        {
            var track = tracks[0];

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
                                new("Position", player.Queue.Count == 0 ? "Now Playing" : $"#{player.Queue.Count}", true),
                            ]
                        }
                    ]
                },
                ct);
        }
        else
        {
            var totalDuration = tracks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);

            await context.Responder.RespondAsync(
                new InteractionResponse
                {
                    Embeds =
                    [
                        new ResponseEmbed
                        {
                            Title = "Playlist added",
                            Description = $"Added **{tracks.Count}** tracks to the queue.",
                            Fields =
                            [
                                new("Total duration", totalDuration.ToString(@"hh\:mm\:ss"), true),
                                new("Queue size", player.Queue.Count.ToString(), true),
                            ]
                        }
                    ]
                },
                ct);
        }

        await unitOfWork.CommitAsync(ct);
    }
}
