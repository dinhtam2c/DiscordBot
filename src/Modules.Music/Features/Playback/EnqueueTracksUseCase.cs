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
    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksInput input, string query, CancellationToken ct)
    {
        notifications.SetChannel(input.GuildId, input.TextChannelId, input.InteractionId);
        var revision = await players.BeginEnqueueAsync(input.GuildId, ct);
        try
        {
            var resolved = await resolverFactory.Select(query).ResolveAsync(query, ct);
            logger.LogInformation("Resolved {TrackCount} tracks for query '{Query}'.", resolved.Tracks.Count, query);
            return await EnqueueAsync(input, resolved, revision, ct);
        }
        finally { await players.EndEnqueueAsync(input.GuildId); }
    }

    public async Task<EnqueueTracksResult> ExecuteAsync(EnqueueTracksInput input, IReadOnlyList<Track> tracks, CancellationToken ct)
    {
        notifications.SetChannel(input.GuildId, input.TextChannelId, input.InteractionId);
        var revision = await players.BeginEnqueueAsync(input.GuildId, ct);
        try { return await EnqueueAsync(input, new TrackResolutionResult(tracks), revision, ct); }
        finally { await players.EndEnqueueAsync(input.GuildId); }
    }

    private async Task<EnqueueTracksResult> EnqueueAsync(EnqueueTracksInput input, TrackResolutionResult resolved,
        long revision, CancellationToken ct)
    {
        var tracks = resolved.Tracks;
        if (tracks.Count == 0)
            throw new UserFriendlyException("Couldn't find any songs. Try another song name or link.");

        var result = await players.ExecuteAsync(input.GuildId,
            new EnqueueTracks(tracks.ToArray(), input.RequesterId, input.Mode == EnqueueTracksMode.Next, revision), ct);

        return new EnqueueTracksResult(tracks, result.QueueCount, result.StartsPlayback, resolved.Playlist);
    }
}
