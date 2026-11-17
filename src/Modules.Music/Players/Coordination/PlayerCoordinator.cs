using Application.Abstractions;
using Application.Discord.User;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Playback;
using Modules.Music.Features.Notifications;
using Modules.Music.Tracks.Youtube;

namespace Modules.Music.Players.Coordination;

internal sealed partial class PlayerCoordinator(
    IServiceScopeFactory scopes,
    IPlaybackEngine playback,
    IYoutubeRecommendations recommendations,
    IVoiceSessionManager voice,
    IVoiceChannelPresence presence,
    MusicNotifications notifications,
    IAppLifetime lifetime,
    IClock clock,
    IOptions<PlaybackHistoryOptions> historyOptions,
    ILogger<PlayerCoordinator> logger) : IHostedService, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<GuildId, PlayerMailbox> _mailboxes = [];
    private readonly CancellationTokenSource _shutdown = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Stopping);
    private bool _stopping;
    private Task? _stopTask;

    public void RefreshVoicePresence(GuildId guildId)
    {
        lock (_sync)
        {
            // Voice events never create players or wait on their mailbox.
            if (!_stopping && _mailboxes.TryGetValue(guildId, out var mailbox))
                mailbox.Post(ct => HandleAsync(guildId, mailbox, new RefreshVoicePresence(), null, ct));
        }
    }

    private bool SynchronizePresence(Player? player)
        => player?.VoiceChannelId is { } channel
            && presence.HasListeners(player.GuildId, channel) is { } hasListeners
            && player.SetHasListeners(hasListeners);

    public Task<PlayerResult> ExecuteAsync(GuildId guildId, PlayerRequest request, CancellationToken ct,
        Guid? expectedQueueItemId = null)
    {
        var mailbox = GetMailbox(guildId);
        return mailbox.SendAsync(token => HandleAsync(guildId, mailbox, request, expectedQueueItemId, token), ct);
    }

    public Task<long> BeginEnqueueAsync(GuildId guildId, CancellationToken ct)
    {
        var mailbox = GetMailbox(guildId);
        return mailbox.SendAsync(_ =>
        {
            ct.ThrowIfCancellationRequested();
            mailbox.Autoplay.PendingEnqueues++;
            mailbox.Autoplay.Cancel();
            return Task.FromResult(mailbox.EnqueueRevision);
        }, _shutdown.Token);
    }

    public Task<IReadOnlyList<PlaybackHistoryEntry>> GetHistoryAsync(GuildId guildId, CancellationToken ct)
    {
        var mailbox = GetMailbox(guildId);
        return mailbox.SendAsync(_ => Task.FromResult(mailbox.History.Snapshot()), ct);
    }

    public Task<SeekPreparation> BeginSeekAsync(GuildId guildId, TimeSpan position, CancellationToken ct)
    {
        var mailbox = GetMailbox(guildId);
        return mailbox.SendAsync(async token =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await repository.GetByIdAsync(guildId, token);
            var item = player?.CurrentQueueItem
                ?? throw new UserFriendlyException("Nothing is playing right now.");
            SeekPlayback.ValidatePosition(position, item.Track.Duration);
            var sessionId = playback.GetSessionId(guildId, item.Id)
                ?? throw new UserFriendlyException("Nothing is playing right now.");
            return new SeekPreparation(item, ++mailbox.SeekRevision, sessionId);
        }, ct);
    }

    public Task<PlayerSnapshot?> GetSnapshotAsync(GuildId guildId, CancellationToken ct)
        => GetMailbox(guildId).SendAsync(async token =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await repository.GetByIdAsync(guildId, token);
            return player is null ? null : PlayerSnapshot.From(player) with
            {
                PlaybackPosition = player.CurrentQueueItem is { } item
                    ? playback.GetPosition(guildId, item.Id)
                    : null,
            };
        }, ct);

    private PlayerMailbox GetMailbox(GuildId guildId)
    {
        lock (_sync)
        {
            if (_stopping || _shutdown.IsCancellationRequested)
                throw new InvalidOperationException("The player coordinator is shutting down.");

            if (!_mailboxes.TryGetValue(guildId, out var mailbox))
            {
                mailbox = new PlayerMailbox(
                    () => playback.ApplyAsync(guildId, PlaybackTarget.From(null), _ => { }, _ => { }, CancellationToken.None),
                    _shutdown.Token, logger, historyOptions.Value.Capacity);
                _mailboxes.Add(guildId, mailbox);
                mailbox.Start();
            }

            return mailbox;
        }
    }

    private async Task<PlayerResult> HandleAsync(GuildId guildId, PlayerMailbox mailbox, PlayerRequest request,
        Guid? expectedQueueItemId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlayerRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var player = await repository.GetByIdAsync(guildId, ct);
        // Validate inside the mailbox, in the same operation that changes the player.
        if (expectedQueueItemId is { } expected && player?.CurrentQueueItem?.Id != expected)
            throw new UserFriendlyException("This song is no longer playing. Use the controls on the latest now-playing message.");

        if (request is SeekPlayback seek)
        {
            if (seek.Preparation.Revision != mailbox.SeekRevision
                || !playback.IsCurrent(guildId, seek.Preparation.SessionId))
                throw new UserFriendlyException("This seek request is no longer current. A newer seek or playback change took its place.");
            var item = player?.CurrentQueueItem
                ?? throw new UserFriendlyException("Nothing is playing right now.");
            if (item.Id != seek.Preparation.Item.Id)
                throw new UserFriendlyException("This song is no longer playing.");
            SeekPlayback.ValidatePosition(seek.Position, item.Track.Duration);
            if (SynchronizePresence(player))
                await unitOfWork.SaveChangesAsync(ct);
            var oldSessionId = playback.GetSessionId(guildId, item.Id);
            await playback.SeekAsync(guildId, PlaybackTarget.From(player), seek.Position, seek.Source, PostPlaybackEnded, ct);
            if (oldSessionId is { } oldId && playback.GetSessionId(guildId, item.Id) is { } newId)
                mailbox.History.Rebind(oldId, newId);
            notifications.Updated(guildId, PlayerSnapshot.From(player!));
            return new PlayerResult(item, player.IsPaused, player.QueueCount, false);
        }

        var previousItem = player?.CurrentQueueItem;
        var previousSessionId = previousItem is { } previous ? playback.GetSessionId(guildId, previous.Id) : null;
        var wasPaused = player?.IsPaused ?? false;
        var wasManuallyPaused = player?.IsManuallyPaused ?? false;
        var previousChannel = player?.VoiceChannelId;
        var existed = player is not null;
        var changed = true;

        if (request is EnqueueTracks { PreparationRevision: { } revision } && revision != mailbox.EnqueueRevision)
            throw new UserFriendlyException("This play request was canceled because the player was stopped or disconnected.");
        if (request is StopPlayback or LeaveVoice)
        {
            mailbox.EnqueueRevision++;
            mailbox.SeekRevision++;
            mailbox.Autoplay.Cancel();
            mailbox.Autoplay.Active = false;
        }

        switch (request)
        {
            case RefreshVoicePresence _:
                changed = false;
                break;
            case JoinVoice join:
                player = await GetOrCreateAsync(player, guildId, repository, ct);
                player.Join(join.ChannelId);
                break;

            case EnqueueTracks enqueue:
                if (enqueue.Tracks.Count == 0)
                    throw new UserFriendlyException("Couldn't find any songs to add. Try another song or link.");

                player = await GetOrCreateAsync(player, guildId, repository, ct);
                if (player.VoiceChannelId is null)
                {
                    var users = services.GetRequiredService<IDiscordUserService>();
                    var channelId = await users.GetUserVoiceChannelIdAsync(guildId, enqueue.RequesterId, ct)
                        ?? throw new UserFriendlyException("You're not in a voice channel. Join one first.");
                    player.Join(channelId);
                }

                var items = enqueue.Tracks.Select(track => new QueueItem(track, enqueue.RequesterId, clock.UtcNow)).ToArray();
                player.Enqueue(items, enqueue.PlayNext);
                break;

            case SetAutoplay:
            case ToggleAutoplay:
                player = await GetOrCreateAsync(player, guildId, repository, ct);
                var enabled = request is SetAutoplay autoplay ? autoplay.Enabled : !player.AutoplayEnabled;
                player.SetAutoplay(enabled);
                if (!enabled)
                {
                    mailbox.Autoplay.Cancel();
                    mailbox.Autoplay.Active = false;
                    mailbox.Autoplay.Ready = null;
                }
                else
                {
                    mailbox.Autoplay.Active = player.HasCurrentTrack;
                    mailbox.Autoplay.Failures = 0;
                }
                break;

            case SetVolume volume:
                player = await GetOrCreateAsync(player, guildId, repository, ct);
                changed = player.SetVolume(volume.Volume) || !existed;
                break;

            case PausePlayback:
                changed = player?.Pause() ?? false;
                break;

            case ResumePlayback:
                changed = player?.Resume() ?? false;
                break;

            case StopPlayback:
                changed = player is not null;
                player?.Stop();
                break;

            case LeaveVoice:
                changed = player is not null;
                if (player is not null)
                {
                    player.Leave();
                    await repository.RemoveAsync(guildId, ct);
                }
                player = null;
                break;

            default:
                if (player is null)
                    throw new UserFriendlyException("Use `/play` to start playing music first.");
                switch (request)
                {
                    case SkipTrack:
                        changed = player.HasCurrentTrack;
                        if (changed) player.Skip();
                        break;
                    case CycleLoopMode:
                        player.SetLoopMode(player.LoopMode switch
                        {
                            LoopMode.None => LoopMode.Track,
                            LoopMode.Track => LoopMode.Queue,
                            _ => LoopMode.None,
                        });
                        break;
                    case SetLoopMode loop:
                        player.SetLoopMode(loop.Mode);
                        break;
                    case ClearQueue:
                        changed = player.HasQueue;
                        player.ClearQueue();
                        break;
                    case ShuffleQueue:
                        changed = player.QueueCount > 1;
                        if (changed) player.ShuffleQueue();
                        break;
                    case RemoveTrack remove:
                        ValidateIndex(remove.Index, player.QueueCount);
                        player.RemoveFromQueue((int)remove.Index - 1);
                        break;
                    case MoveTrack move:
                        ValidateIndex(move.Index, player.QueueCount);
                        ValidateIndex(move.NewIndex, player.QueueCount, "new position");
                        player.MoveInQueue((int)move.Index - 1, (int)move.NewIndex - 1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request), "The player request type is not supported.");
                }
                break;
        }

        changed |= SynchronizePresence(player);
        if (request is RefreshVoicePresence && !changed)
        {
            SynchronizeAutoplay(guildId, mailbox, player);
            return new PlayerResult(previousItem, wasPaused, player?.QueueCount ?? 0, false);
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(ct);

        if (previousItem is not null && previousSessionId is { } sessionId
            && request is SkipTrack or StopPlayback or LeaveVoice)
            mailbox.History.Finish(sessionId, previousItem,
                request is SkipTrack ? PlaybackHistoryStatus.Skipped : PlaybackHistoryStatus.Stopped, clock.UtcNow);

        // Persist first. The mailbox holds the next request until runtime has accepted this target.
        var endedPosition = previousItem is not null && player?.CurrentQueueItem is null
            ? playback.GetPosition(guildId, previousItem.Id)
            : null;
        await ApplyAsync(guildId, player, previousChannel, request is JoinVoice, request is LeaveVoice, ct);
        if (changed && previousItem is not null)
        {
            if (player?.CurrentQueueItem is null)
                notifications.Ended(guildId, stopped: request is StopPlayback or LeaveVoice, position: endedPosition);
            else if (player.CurrentQueueItem.Id == previousItem.Id)
                notifications.Updated(guildId, PlayerSnapshot.From(player));
        }
        SynchronizeAutoplay(guildId, mailbox, player);
        return new PlayerResult(previousItem, wasPaused, player?.QueueCount ?? 0,
            previousItem is null && player?.HasCurrentTrack == true)
        {
            IsPausedForEmptyChannel = player?.IsPausedForEmptyChannel ?? false,
            WasManuallyPaused = wasManuallyPaused,
        };
    }

    private async Task ApplyAsync(
        GuildId guildId, Player? player, ChannelId? previousChannel, bool explicitJoin, bool explicitLeave, CancellationToken ct)
    {
        await voice.RequestStatusAsync(guildId, player?.CurrentQueueItem?.Track.Title, ct);
        if (player?.VoiceChannelId is { } channel)
        {
            if (explicitJoin || previousChannel != channel)
                await voice.RequestJoinAsync(guildId, channel, ct);

            await playback.ApplyAsync(guildId, PlaybackTarget.From(player), PostPlaybackStarted, PostPlaybackEnded, ct);
            if (player.CurrentQueueItem is { } item && playback.GetSessionId(guildId, item.Id) is { } sessionId)
                GetMailbox(guildId).History.Start(sessionId, item, clock.UtcNow);
        }
        else
        {
            await playback.ApplyAsync(guildId, PlaybackTarget.From(null), PostPlaybackStarted, PostPlaybackEnded, ct);
            if (explicitLeave || previousChannel is not null)
                await voice.RequestLeaveAsync(guildId, ct);
        }
    }

    private void PostPlaybackStarted(PlaybackStarted started)
    {
        lock (_sync)
        {
            if (!_stopping && _mailboxes.TryGetValue(started.GuildId, out var mailbox))
                mailbox.Post(ct => HandleStartedAsync(started, ct));
        }
    }

    private async Task HandleStartedAsync(PlaybackStarted started, CancellationToken ct)
    {
        if (!playback.IsCurrent(started.GuildId, started.SessionId))
            return;

        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
        var player = await repository.GetByIdAsync(started.GuildId, ct);
        if (player?.CurrentQueueItem is { } item && item.Id == started.QueueItemId)
        {
            notifications.Playing(started.GuildId, PlayerSnapshot.From(player));
            SynchronizeAutoplay(started.GuildId, GetMailbox(started.GuildId), player);
        }
    }

    private void PostPlaybackEnded(PlaybackEnded ended)
    {
        var pending = new PendingCompletion(ended);
        lock (_sync)
        {
            if (!_stopping && _mailboxes.TryGetValue(ended.GuildId, out var mailbox))
                mailbox.Post(ct => HandleEndedAsync(pending, ct));
        }
    }

    private async Task HandleEndedAsync(PendingCompletion pending, CancellationToken ct)
    {
        var ended = pending.Ended;
        try
        {
            if (!pending.Saved && !playback.IsCurrent(ended.GuildId, ended.SessionId))
                return;

            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await repository.GetByIdAsync(ended.GuildId, ct);
            if (!pending.Saved)
            {
                QueueItem? failedItem = null;
                if (player?.CurrentQueueItem is { } item && item.Id == ended.QueueItemId)
                {
                    if (ended.Reason == PlaybackEndReason.Failed)
                    {
                        failedItem = item;
                        player.Skip();
                    }
                    else
                        player.FinishCurrentTrack();
                    SynchronizePresence(player);
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                    GetMailbox(ended.GuildId).History.Finish(ended.SessionId, item,
                        ended.Reason == PlaybackEndReason.Failed ? PlaybackHistoryStatus.Failed : PlaybackHistoryStatus.Completed,
                        clock.UtcNow);
                }
                pending.Saved = true;
                var autoplay = GetMailbox(ended.GuildId).Autoplay;
                if (failedItem is not null)
                {
                    if (failedItem.IsAutoplay) autoplay.Failures = Math.Min(autoplay.Failures + 1, 30);
                    else notifications.Failed(ended.GuildId, failedItem, ended.Error);
                }
                else if (ended.Reason == PlaybackEndReason.Completed) autoplay.Failures = 0;
                if (player?.HasCurrentTrack == false)
                    notifications.Ended(ended.GuildId, stopped: false,
                        position: playback.GetPosition(ended.GuildId, ended.QueueItemId),
                        completed: ended.Reason == PlaybackEndReason.Completed);
            }

            // Once saved, retries only synchronize the latest stored target. Never advance twice.
            if (SynchronizePresence(player))
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            await playback.CompleteAsync(ended.GuildId, ended.SessionId);
            await ApplyAsync(ended.GuildId, player, player?.VoiceChannelId, false, false, ct);
            SynchronizeAutoplay(ended.GuildId, GetMailbox(ended.GuildId), player);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle playback completion for guild {GuildId}. Retrying in 1 second.", ended.GuildId);
            _ = RetryCompletionAsync(pending, ct);
        }
    }

    private async Task RetryCompletionAsync(PendingCompletion pending, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            lock (_sync)
            {
                if (!_stopping && _mailboxes.TryGetValue(pending.Ended.GuildId, out var mailbox))
                    mailbox.Post(token => HandleEndedAsync(pending, token));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private sealed class PendingCompletion(PlaybackEnded ended)
    {
        public PlaybackEnded Ended { get; } = ended;
        public bool Saved { get; set; }
    }

    private static async Task<Player> GetOrCreateAsync(Player? player, GuildId guildId, IPlayerRepository repository, CancellationToken ct)
    {
        if (player is not null) return player;
        player = Player.Create(guildId);
        await repository.AddAsync(player, ct);
        return player;
    }

    private static void ValidateIndex(long index, int count, string name = "queue position")
    {
        if (index < 1 || index > count)
            throw new UserFriendlyException(count == 0
                ? "The queue is empty. Add some songs with `/play` first."
                : $"Enter a {name} from 1 to {count}. Check `/queue` for song numbers.");
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_stopTask is not null)
                return _stopTask.WaitAsync(ct);

            _stopping = true;
            foreach (var mailbox in _mailboxes.Values) mailbox.Complete();
            var pending = _mailboxes.Values.Select(mailbox => mailbox.Completion).ToArray();
            _stopTask = StopCoreAsync(pending);
            return _stopTask.WaitAsync(ct);
        }
    }

    private async Task StopCoreAsync(Task[] pending)
    {
        await _shutdown.CancelAsync();
        await Task.WhenAll(pending);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _shutdown.Dispose();
    }
}
