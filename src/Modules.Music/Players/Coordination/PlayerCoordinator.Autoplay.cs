using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Music.Tracks;
using Modules.Music.Tracks.Youtube;
using Application.Abstractions;

namespace Modules.Music.Players.Coordination;

internal sealed partial class PlayerCoordinator
{
    public async Task EndEnqueueAsync(GuildId guildId)
    {
        PlayerMailbox mailbox;
        lock (_sync)
        {
            if (_stopping || !_mailboxes.TryGetValue(guildId, out mailbox!)) return;
        }
        await mailbox.SendAsync(async ct =>
        {
            mailbox.Autoplay.PendingEnqueues = Math.Max(0, mailbox.Autoplay.PendingEnqueues - 1);
            await using var scope = scopes.CreateAsyncScope();
            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetByIdAsync(guildId, ct);
            SynchronizeAutoplay(guildId, mailbox, player);
            return true;
        }, _shutdown.Token);
    }

    private void SynchronizeAutoplay(GuildId guildId, PlayerMailbox mailbox, Player? player)
    {
        var state = mailbox.Autoplay;
        if (player is null)
        {
            state.Cancel();
            state.Active = false;
            state.ResetSeed();
            return;
        }
        state.Observe(player.CurrentQueueItem);
        if (!player.AutoplayEnabled || !state.Active || state.PendingEnqueues > 0
            || player.VoiceChannelId is not { } channel || presence.HasListeners(guildId, channel) != true
            || player.HasQueue)
        {
            state.Cancel();
            return;
        }
        if (state.Ready is { } ready && IsExcluded(mailbox, ready)) state.Ready = null;
        if (state.Ready is not null)
        {
            if (!player.HasCurrentTrack)
            {
                var readyRevision = state.Revision;
                mailbox.Post(ct => AcceptAutoplayAsync(guildId, mailbox, readyRevision, ct));
            }
            return;
        }
        if (state.Work is not null || state.Seed is null) return;
        var seed = state.Seed;
        var history = mailbox.History.RecentTracks(historyOptions.Value.AutoplayWindow);
        var candidates = state.Candidates.ToArray();
        var discovery = state.LoadedLimit >= 75 && candidates.Length == 0;
        var attempt = state.DiscoveryAttempt;
        var pivots = history.Where(track => track.Source == TrackSource.Youtube
            && !YoutubeRecommendations.SameSong(seed, track)).Reverse().DistinctBy(track => track.Id).ToArray();
        var discoverySeed = discovery && pivots.Length > 0 ? pivots[attempt % pivots.Length] : seed;
        var failures = state.Failures;
        var limit = state.LoadedLimit == 0 ? 25 : 75;
        var (revision, work) = state.Begin(_shutdown.Token);
        state.Preparation = PrepareAutoplayAsync(guildId, mailbox, seed, history, candidates, discoverySeed, discovery, attempt, failures, limit, revision, work);
    }

    private async Task PrepareAutoplayAsync(GuildId guildId, PlayerMailbox mailbox, Track seed,
        Track[] history, Track[] cached, Track discoverySeed, bool discovery, int attempt, int failures, int limit, long revision, CancellationTokenSource work)
    {
        Track? selected = null;
        Exception? error = null;
        var remaining = new Queue<Track>();
        try
        {
            var ct = work.Token;
            if (failures > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 1 << Math.Min(failures - 1, 5))), ct);
            // No context or mutable session leaves the mailbox.
            var tracks = cached.Length > 0 ? cached : discovery
                ? await recommendations.ExploreAsync(discoverySeed, attempt, ct)
                : await recommendations.GetAsync(seed, limit, ct);
            var available = tracks.ToArray();
            remaining = new Queue<Track>(available.Where(track => !YoutubeRecommendations.SameSong(seed, track) && !history.Any(old => YoutubeRecommendations.SameSong(old, track))));
            var attempts = 0;
            while (attempts++ < 5 && remaining.TryDequeue(out var candidate))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var resolved = await recommendations.ResolveAsync(candidate, ct);
                    if (resolved is not null && !YoutubeRecommendations.SameSong(seed, resolved) && YoutubeRecommendations.Suitable(seed, resolved)
                        && !history.Any(old => YoutubeRecommendations.SameSong(old, resolved)))
                    { selected = resolved; break; }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogDebug(ex, "Rejected autoplay candidate '{TrackTitle}'.", candidate.Title); }
            }
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested) { return; }
        catch (Exception ex) { error = ex; }
        finally
        {
            // Await mailbox acceptance before disposing the token source it may still cancel.
            if (!work.IsCancellationRequested)
            {
                try
                {
                    await mailbox.SendAsync(async ct =>
                    {
                        var state = mailbox.Autoplay;
                        if (state.Revision != revision) return false;
                        state.Complete();
                        if (error is not null)
                        {
                            logger.LogDebug(error, "Autoplay recommendations are unavailable for guild {GuildId}. Retrying discovery.", guildId);
                        }
                        if (cached.Length == 0) state.LoadedLimit = limit;
                        state.Candidates.Clear();
                        foreach (var track in remaining) state.Candidates.Enqueue(track);
                        state.Ready = selected;
                        if (discovery) state.DiscoveryAttempt = (attempt + 1) % 10000;
                        if (selected is null) state.Failures = Math.Min(state.Failures + 1, 30);
                        await AcceptAutoplayAsync(guildId, mailbox, revision, ct);
                        return true;
                    }, _shutdown.Token);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
                catch (Exception ex) { logger.LogError(ex, "Failed to accept autoplay for guild {GuildId}.", guildId); }
            }
            work.Dispose();
        }
    }

    private async Task AcceptAutoplayAsync(GuildId guildId, PlayerMailbox mailbox, long revision, CancellationToken ct)
    {
        var state = mailbox.Autoplay;
        if (state.Revision != revision) return;
        await using var scope = scopes.CreateAsyncScope();
        var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetByIdAsync(guildId, ct);
        if (player is null || !player.AutoplayEnabled || !state.Active || state.PendingEnqueues > 0
            || player.HasCurrentTrack || player.HasQueue || player.VoiceChannelId is not { } channel
            || presence.HasListeners(guildId, channel) != true)
            return;
        if (state.Ready is not { } track || IsExcluded(mailbox, track))
        {
            state.Ready = null;
            SynchronizeAutoplay(guildId, mailbox, player);
            return;
        }
        player.Enqueue([new QueueItem(track, null, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow)], false);
        SynchronizePresence(player);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        state.Ready = null;
        state.Observe(player.CurrentQueueItem);
        await ApplyAsync(guildId, player, channel, false, false, ct);
        SynchronizeAutoplay(guildId, mailbox, player);
    }

    private bool IsExcluded(PlayerMailbox mailbox, Track track)
        => mailbox.Autoplay.Seed is { } seed && YoutubeRecommendations.SameSong(seed, track)
            || mailbox.History.RecentTracks(historyOptions.Value.AutoplayWindow).Any(previous => YoutubeRecommendations.SameSong(previous, track));

}
