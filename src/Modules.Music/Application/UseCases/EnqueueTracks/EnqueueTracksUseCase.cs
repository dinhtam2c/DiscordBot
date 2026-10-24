using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.User;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.UseCases.EnqueueTracks;

internal enum EnqueueTracksMode
{
    Queue,
    Next,
}

internal sealed record EnqueueTracksRequest(
    GuildId GuildId,
    UserId RequesterId,
    string Query,
    EnqueueTracksMode Mode);

internal sealed record EnqueueTracksResult(
    IReadOnlyList<Track> Tracks,
    int QueueCount,
    bool StartsPlayback);

internal sealed class EnqueueTracksUseCase(
    IPlayerRepository playerRepository,
    IDiscordUserService discordUserService,
    ITrackResolverFactory resolverFactory,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<EnqueueTracksUseCase> logger)
{
    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksRequest request, CancellationToken ct)
    {
        var player = await playerRepository.GetByIdAsync(request.GuildId, ct);
        if (player is null)
        {
            player = Player.Create(request.GuildId);
            await playerRepository.AddAsync(player, ct);
        }

        if (player.VoiceChannelId is null)
        {
            var voiceChannelInfo = await discordUserService.GetUserVoiceChannelAsync(
                    request.GuildId,
                    request.RequesterId,
                    ct)
                ?? throw new UserFriendlyException("You are not in a voice channel.");

            player.Join(voiceChannelInfo.Id);
        }

        var tracks = await resolverFactory.Select(request.Query).ResolveAsync(request.Query, ct);
        if (tracks.Count == 0)
            throw new UserFriendlyException($"No track found for query: {request.Query}");

        logger.LogInformation(
            "Resolved {TrackCount} tracks for query: {Query}",
            tracks.Count,
            request.Query);

        var startsPlayback = !player.HasCurrentTrack;
        if (request.Mode == EnqueueTracksMode.Queue)
        {
            foreach (var track in tracks)
                player.Play(CreateQueueItem(track, request.RequesterId));
        }
        else
        {
            EnqueueNext(player, tracks, request.RequesterId);
        }

        var queueCount = player.Queue.Count;
        await unitOfWork.CommitAsync(ct);

        return new EnqueueTracksResult(tracks, queueCount, startsPlayback);
    }

    private QueueItem CreateQueueItem(Track track, UserId requesterId)
        => new(track, requesterId, clock.UtcNow);

    private void EnqueueNext(Player player, IReadOnlyList<Track> tracks, UserId requesterId)
    {
        if (player.HasCurrentTrack)
        {
            foreach (var track in tracks.Reverse())
                player.PlayNext(CreateQueueItem(track, requesterId));

            return;
        }

        player.Play(CreateQueueItem(tracks[0], requesterId));
        foreach (var track in tracks.Skip(1).Reverse())
            player.PlayNext(CreateQueueItem(track, requesterId));
    }
}
