using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.User;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Music.Playback;

namespace Modules.Music.Players.Coordination;

internal sealed class PlayerCoordinator(
    IServiceScopeFactory scopes,
    IPlaybackEngine playback,
    IVoiceSessionManager voice,
    IAppLifetime lifetime,
    ILogger<PlayerCoordinator> logger) : IHostedService, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<GuildId, PlayerMailbox> _mailboxes = [];
    private readonly CancellationTokenSource _shutdown = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Stopping);
    private bool _stopping;
    private Task? _stopTask;

    public Task<PlayerResult> ExecuteAsync(GuildId guildId, PlayerRequest request, CancellationToken ct)
        => GetMailbox(guildId).SendAsync(token => HandleAsync(guildId, request, token), ct);

    public Task<PlayerSnapshot?> GetSnapshotAsync(GuildId guildId, CancellationToken ct)
        => GetMailbox(guildId).SendAsync(async token =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await repository.GetByIdAsync(guildId, token);
            return player is null ? null : PlayerSnapshot.From(player);
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
                    () => playback.ApplyAsync(guildId, PlaybackTarget.From(null), _ => { }, CancellationToken.None),
                    _shutdown.Token, logger);
                _mailboxes.Add(guildId, mailbox);
                mailbox.Start();
            }

            return mailbox;
        }
    }

    private async Task<PlayerResult> HandleAsync(GuildId guildId, PlayerRequest request, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlayerRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var player = await repository.GetByIdAsync(guildId, ct);
        var before = player is null ? null : PlayerSnapshot.From(player);
        var changed = true;

        switch (request)
        {
            case JoinVoice join:
                player = await GetOrCreateAsync(player, guildId, repository, ct);
                player.Join(join.ChannelId);
                break;

            case EnqueueTracks enqueue:
                if (enqueue.Tracks.Count == 0)
                    throw new UserFriendlyException("No tracks to enqueue.");

                player = await GetOrCreateAsync(player, guildId, repository, ct);
                if (player.VoiceChannelId is null)
                {
                    var users = services.GetRequiredService<IDiscordUserService>();
                    var channel = await users.GetUserVoiceChannelAsync(guildId, enqueue.RequesterId, ct)
                        ?? throw new UserFriendlyException("You are not in a voice channel.");
                    player.Join(channel.Id);
                }

                var clock = services.GetRequiredService<IClock>();
                var items = enqueue.Tracks.Select(track => new QueueItem(track, enqueue.RequesterId, clock.UtcNow)).ToArray();
                if (!enqueue.PlayNext)
                {
                    foreach (var item in items)
                        player.Play(item);
                }
                else
                {
                    var start = 0;
                    if (!player.HasCurrentTrack)
                    {
                        player.Play(items[0]);
                        start = 1;
                    }
                    for (var i = items.Length - 1; i >= start; i--)
                        player.PlayNext(items[i]);
                }
                break;

            case SetVolume volume:
                player = await GetOrCreateAsync(player, guildId, repository, ct);
                changed = player.SetVolume(volume.Volume) || before is null;
                break;

            case PausePlayback:
                changed = player?.Pause() ?? false;
                break;

            case ResumePlayback:
                changed = player?.Resume() ?? false;
                break;

            case LeaveVoice:
                if (player is null)
                    throw new UserFriendlyException("Not in a voice channel for playing music.");
                player.Leave();
                await repository.RemoveAsync(guildId, ct);
                player = null;
                break;

            default:
                if (player is null)
                    throw new UserFriendlyException("Player not found. Start playing music first.");
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
                        ValidateIndex(move.NewIndex, player.QueueCount, "new index");
                        player.MoveInQueue((int)move.Index - 1, (int)move.NewIndex - 1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request));
                }
                break;
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(ct);

        // Persist first. The mailbox holds the next request until runtime has accepted this target.
        await ApplyAsync(guildId, player, before, request is JoinVoice, request is LeaveVoice, ct);
        return new PlayerResult(before, player is null ? null : PlayerSnapshot.From(player));
    }

    private async Task ApplyAsync(
        GuildId guildId, Player? player, PlayerSnapshot? before, bool explicitJoin, bool explicitLeave, CancellationToken ct)
    {
        if (player?.VoiceChannelId is { } channel)
        {
            if (explicitJoin || before?.VoiceChannelId != channel)
                await voice.RequestJoinAsync(guildId, channel, ct);

            await playback.ApplyAsync(guildId, PlaybackTarget.From(player), PostPlaybackEnded, ct);
        }
        else
        {
            await playback.ApplyAsync(guildId, PlaybackTarget.From(null), PostPlaybackEnded, ct);
            if (explicitLeave || before?.VoiceChannelId is not null)
                await voice.RequestLeaveAsync(guildId, ct);
        }
    }

    private void PostPlaybackEnded(PlaybackEnded ended)
    {
        lock (_sync)
        {
            if (!_stopping && _mailboxes.TryGetValue(ended.GuildId, out var mailbox))
                mailbox.Post(ct => HandleEndedAsync(ended, ct));
        }
    }

    private async Task HandleEndedAsync(PlaybackEnded ended, CancellationToken ct)
    {
        if (!playback.IsCurrent(ended.GuildId, ended.SessionId))
            return;

        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
        var player = await repository.GetByIdAsync(ended.GuildId, ct);
        if (player is not null && player.HasCurrentTrack)
        {
            player.FinishCurrentTrack();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        // Retire the ended session before applying: track-loop intentionally keeps the same queue item.
        await playback.CompleteAsync(ended.GuildId, ended.SessionId);
        await playback.ApplyAsync(ended.GuildId, PlaybackTarget.From(player), PostPlaybackEnded, ct);
    }

    private static async Task<Player> GetOrCreateAsync(Player? player, GuildId guildId, IPlayerRepository repository, CancellationToken ct)
    {
        if (player is not null) return player;
        player = Player.Create(guildId);
        await repository.AddAsync(player, ct);
        return player;
    }

    private static void ValidateIndex(long index, int count, string name = "index")
    {
        if (index < 1 || index > count)
            throw new UserFriendlyException($"Invalid {name}. Please provide a number between 1 and {count}.");
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
