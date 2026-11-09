using Application.Discord.Interactions;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Modules.Music.Players.Coordination;
using Modules.Music.Tracks;
using Modules.Music.Tracks.Resolution;

namespace Modules.Music.Features.EnqueueTracks;

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
    ITrackResolverFactory resolverFactory,
    PlayerCoordinator players,
    ILogger<EnqueueTracksUseCase> logger)
{
    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksRequest request, CancellationToken ct)
    {
        // External metadata resolution does not block a guild's controls or capture a Player/context.
        var tracks = await resolverFactory.Select(request.Query).ResolveAsync(request.Query, ct);
        if (tracks.Count == 0)
            throw new UserFriendlyException($"No track found for query: {request.Query}");

        logger.LogInformation("Resolved {TrackCount} tracks for query: {Query}", tracks.Count, request.Query);
        var result = await players.ExecuteAsync(request.GuildId,
            new Players.Coordination.EnqueueTracks(tracks.ToArray(), request.RequesterId, request.Mode == EnqueueTracksMode.Next), ct);

        return new EnqueueTracksResult(tracks, result.After!.Queue.Count, result.Before?.HasCurrentTrack != true);
    }
}
