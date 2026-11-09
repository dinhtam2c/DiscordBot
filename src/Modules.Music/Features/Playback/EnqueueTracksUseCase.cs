using Application.Abstractions;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Modules.Music.Players.Coordination;
using Modules.Music.Features.Notifications;
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
    ChannelId TextChannelId,
    ulong InteractionId,
    string Query,
    EnqueueTracksMode Mode);

internal sealed record EnqueueTracksResult(
    IReadOnlyList<Track> Tracks,
    int QueueCount,
    bool StartsPlayback,
    PlaylistInfo? Playlist);

internal sealed class EnqueueTracksUseCase(
    ITrackResolverFactory resolverFactory,
    PlayerCoordinator players,
    MusicNotifications notifications,
    ILogger<EnqueueTracksUseCase> logger)
{
    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksInput input, CancellationToken ct)
    {
        notifications.SetChannel(input.GuildId, input.TextChannelId, input.InteractionId);
        var resolved = await resolverFactory.Select(input.Query).ResolveAsync(input.Query, ct);
        var tracks = resolved.Tracks;
        if (tracks.Count == 0)
            throw new UserFriendlyException("Couldn't find any songs. Try another song name or link.");

        logger.LogInformation("Resolved {TrackCount} tracks for query '{Query}'.", tracks.Count, input.Query);
        var result = await players.ExecuteAsync(input.GuildId,
            new EnqueueTracks(tracks.ToArray(), input.RequesterId, input.Mode == EnqueueTracksMode.Next), ct);

        return new EnqueueTracksResult(tracks, result.QueueCount, result.StartsPlayback, resolved.Playlist);
    }
}
