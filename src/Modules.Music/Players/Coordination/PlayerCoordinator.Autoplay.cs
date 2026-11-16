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
        if (state.Failures >= 3)
        {
            StopAutoplay(guildId, mailbox);
            return;
        }
        if (state.Ready is { } ready && state.Excludes(ready)) state.Ready = null;
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
        if (state.Candidates.Count == 0 && state.LoadedLimit >= 75)
        {
            if (!player.HasCurrentTrack) StopAutoplay(guildId, mailbox);
            return;
        }
        var seed = state.Seed;
        var history = state.History.ToArray();
        var candidates = state.Candidates.ToArray();
        var limit = state.LoadedLimit == 0 ? 25 : 75;
        var (revision, work) = state.Begin(_shutdown.Token);
        state.Preparation = PrepareAutoplayAsync(guildId, mailbox, seed, history, candidates, limit, revision, work);
    }

    private async Task PrepareAutoplayAsync(GuildId guildId, PlayerMailbox mailbox, Track seed,
        Track[] history, Track[] cached, int limit, long revision, CancellationTokenSource work)
    {
        Track? selected = null;
        Exception? error = null;
        var remaining = new Queue<Track>();
        try
        {
            var ct = work.Token;
            // No context or mutable session leaves the mailbox.
            var tracks = cached.Length > 0 ? cached : await recommendations.GetAsync(seed, limit, ct);
            remaining = new Queue<Track>(tracks.Where(track => !history.Any(old => YoutubeRecommendations.SameSong(old, track))));
            var attempts = 0;
            while (attempts++ < 5 && remaining.TryDequeue(out var candidate))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var resolved = await recommendations.ResolveAsync(candidate, ct);
                    if (resolved is not null && YoutubeRecommendations.Suitable(seed, resolved)
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
                            logger.LogWarning(error, "Failed to prepare autoplay for guild {GuildId}.", guildId);
                            StopAutoplay(guildId, mailbox);
                            return false;
                        }
                        if (cached.Length == 0) state.LoadedLimit = limit;
                        state.Candidates.Clear();
                        foreach (var track in remaining) state.Candidates.Enqueue(track);
                        state.Ready = selected;
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
        if (state.Ready is not { } track || state.Excludes(track))
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

    private void StopAutoplay(GuildId guildId, PlayerMailbox mailbox)
    {
        var state = mailbox.Autoplay;
        state.Cancel();
        state.Active = false;
        state.Ready = null;
        notifications.AutoplayStopped(guildId);
    }
}
