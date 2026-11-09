using Application.Abstractions;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Modules.Music.Players.Coordination;
using Modules.Music.Tracks;
using Modules.Music.Tracks.Resolution;

namespace Modules.Music.Features.Playback;

internal enum EnqueueTracksMode
{
    Queue,
    Next,
}

internal sealed record EnqueueTracksInput(
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
    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksInput input, CancellationToken ct)
    {
        var tracks = await resolverFactory.Select(input.Query).ResolveAsync(input.Query, ct);
        if (tracks.Count == 0)
            throw new UserFriendlyException($"No track found for query: {input.Query}");

        logger.LogInformation("Resolved {TrackCount} tracks for query: {Query}", tracks.Count, input.Query);
        var result = await players.ExecuteAsync(input.GuildId,
            new EnqueueTracks(tracks.ToArray(), input.RequesterId, input.Mode == EnqueueTracksMode.Next), ct);

        return new EnqueueTracksResult(tracks, result.QueueCount, result.StartsPlayback);
    }
}
