using Application.Abstractions;
using Application.Discord.User;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Music.Playback;
using Modules.Music.Features.Notifications;

namespace Modules.Music.Players.Coordination;

internal sealed class PlayerCoordinator(
    IServiceScopeFactory scopes,
    IPlaybackEngine playback,
    IVoiceSessionManager voice,
    MusicNotifications notifications,
    IAppLifetime lifetime,
    ILogger<PlayerCoordinator> logger) : IHostedService, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<GuildId, PlayerMailbox> _mailboxes = [];
    private readonly CancellationTokenSource _shutdown = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Stopping);
    private bool _stopping;
    private Task? _stopTask;

    public Task<PlayerResult> ExecuteAsync(GuildId guildId, PlayerRequest request, CancellationToken ct,
        Guid? expectedQueueItemId = null)
        => GetMailbox(guildId).SendAsync(token => HandleAsync(guildId, request, expectedQueueItemId, token), ct);

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
                    _shutdown.Token, logger);
                _mailboxes.Add(guildId, mailbox);
                mailbox.Start();
            }

            return mailbox;
        }
    }

    private async Task<PlayerResult> HandleAsync(GuildId guildId, PlayerRequest request, Guid? expectedQueueItemId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlayerRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var player = await repository.GetByIdAsync(guildId, ct);
        // Validate inside the mailbox, in the same operation that changes the player.
        if (expectedQueueItemId is { } expected && player?.CurrentQueueItem?.Id != expected)
            throw new UserFriendlyException("This song is no longer playing. Use the controls on the latest now-playing message.");

        var previousItem = player?.CurrentQueueItem;
        var wasPaused = player?.IsPaused ?? false;
        var previousChannel = player?.VoiceChannelId;
        var existed = player is not null;
        var changed = true;

        switch (request)
        {
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

                var clock = services.GetRequiredService<IClock>();
                var items = enqueue.Tracks.Select(track => new QueueItem(track, enqueue.RequesterId, clock.UtcNow)).ToArray();
                player.Enqueue(items, enqueue.PlayNext);
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

            case LeaveVoice:
                if (player is null)
                    throw new UserFriendlyException("Use `/play` to start playing music first.");
                player.Leave();
                await repository.RemoveAsync(guildId, ct);
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
                    case StopPlayback:
                        player.Stop();
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

        if (changed)
            await unitOfWork.SaveChangesAsync(ct);

        // Persist first. The mailbox holds the next request until runtime has accepted this target.
        await ApplyAsync(guildId, player, previousChannel, request is JoinVoice, request is LeaveVoice, ct);
        if (changed && previousItem is not null)
        {
            if (player?.CurrentQueueItem is null)
                notifications.Ended(guildId, stopped: request is StopPlayback or LeaveVoice);
            else if (player.CurrentQueueItem.Id == previousItem.Id && player.IsPaused != wasPaused)
                notifications.Updated(guildId, player.CurrentQueueItem, player.IsPaused);
        }
        return new PlayerResult(previousItem, wasPaused, player?.QueueCount ?? 0,
            previousItem is null && player?.HasCurrentTrack == true);
    }

    private async Task ApplyAsync(
        GuildId guildId, Player? player, ChannelId? previousChannel, bool explicitJoin, bool explicitLeave, CancellationToken ct)
    {
        if (player?.VoiceChannelId is { } channel)
        {
            if (explicitJoin || previousChannel != channel)
                await voice.RequestJoinAsync(guildId, channel, ct);

            await playback.ApplyAsync(guildId, PlaybackTarget.From(player), PostPlaybackStarted, PostPlaybackEnded, ct);
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
            notifications.Playing(started.GuildId, item, player.IsPaused);
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
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                }
                pending.Saved = true;
                if (failedItem is not null)
                    notifications.Failed(ended.GuildId, failedItem, ended.Error);
                if (player?.HasCurrentTrack == false)
                    notifications.Ended(ended.GuildId, stopped: false);
            }

            // Once saved, retries only synchronize the latest stored target. Never advance twice.
            await playback.CompleteAsync(ended.GuildId, ended.SessionId);
            await playback.ApplyAsync(ended.GuildId, PlaybackTarget.From(player), PostPlaybackStarted, PostPlaybackEnded, ct);
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
