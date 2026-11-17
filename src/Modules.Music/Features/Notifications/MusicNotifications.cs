using System.Collections.Concurrent;
using System.Threading.Channels;
using Application.Abstractions;
using Application.Discord.Messages;
using Discord;
using Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Music.Playback;
using Modules.Music.Features.Playback;
using Modules.Music.Players;

namespace Modules.Music.Features.Notifications;

internal sealed class MusicNotifications(
    DiscordMessageQueue messages,
    IPlaybackEngine playback,
    ILogger<MusicNotifications> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<GuildId, Destination> _destinations = new();
    private readonly Dictionary<GuildId, SentMessage> _lastPlaying = new();
    private readonly Channel<Notification> _pending = Channel.CreateUnbounded<Notification>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    public void SetChannel(GuildId guildId, ChannelId channelId, ulong interactionId)
    {
        var destination = new Destination(channelId, interactionId);
        // An older interaction may reach its handler after a newer one has already run.
        _destinations.AddOrUpdate(guildId, destination,
            (_, current) => interactionId > current.InteractionId ? destination : current);
    }

    public void Playing(GuildId guildId, PlayerSnapshot player)
    {
        if (_destinations.TryGetValue(guildId, out var destination) && player.CurrentQueueItem is { } item)
            Publish(new Notification(guildId, NotificationKind.Started, destination.ChannelId,
                Position: playback.GetPosition(guildId, item.Id), Player: player));
    }

    public void Updated(GuildId guildId, PlayerSnapshot player)
    {
        if (player.CurrentQueueItem is { } item)
            Publish(new Notification(guildId, NotificationKind.Updated,
                Position: playback.GetPosition(guildId, item.Id), Player: player));
    }

    public void Ended(GuildId guildId, bool stopped, TimeSpan? position = null, bool completed = false)
        => Publish(new Notification(guildId,
            stopped ? NotificationKind.Stopped : completed ? NotificationKind.Completed : NotificationKind.Finished,
            Position: position));

    public void Failed(GuildId guildId, QueueItem item, Exception? error)
    {
        if (_destinations.TryGetValue(guildId, out var destination))
            Publish(new Notification(guildId, NotificationKind.Failed, destination.ChannelId, item, Error: error));
    }

    private void Publish(Notification notification)
    {
        if (!_pending.Writer.TryWrite(notification))
            logger.LogWarning("Failed to queue a music notification for guild {GuildId}. The notification queue is closed.",
                notification.GuildId);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var nextNotification = _pending.Reader.WaitToReadAsync(ct).AsTask();
        var nextTick = timer.WaitForNextTickAsync(ct).AsTask();
        try
        {
            while (true)
            {
                await Task.WhenAny(nextNotification, nextTick);
                if (nextNotification.IsCompleted)
                {
                    if (!await nextNotification)
                        break;
                    while (_pending.Reader.TryRead(out var notification))
                        await DeliverSafelyAsync(notification, ct);
                    nextNotification = _pending.Reader.WaitToReadAsync(ct).AsTask();
                }
                if (nextTick.IsCompleted)
                {
                    if (!await nextTick)
                        break;
                    await RefreshProgressAsync(ct);
                    nextTick = timer.WaitForNextTickAsync(ct).AsTask();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { _pending.Writer.TryComplete(); }
    }

    public override Task StopAsync(CancellationToken ct)
    {
        _pending.Writer.TryComplete();
        return base.StopAsync(ct);
    }

    private async Task DeliverSafelyAsync(Notification notification, CancellationToken ct)
    {
        try { await DeliverAsync(notification, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish a music notification for guild {GuildId}.", notification.GuildId);
        }
    }

    private async Task RefreshProgressAsync(CancellationToken ct)
    {
        // The same reader owns both timer updates and notifications, including message replacement.
        foreach (var (guildId, message) in _lastPlaying.ToArray())
        {
            if (!message.IsActive || message.IsPaused)
                continue;
            var position = playback.GetPosition(guildId, message.Item.Id);
            if (position is null || position == message.Position)
                continue;
            await DeliverSafelyAsync(new Notification(guildId, NotificationKind.Updated,
                Position: position, Player: message.Player), ct);
        }
    }

    private async Task DeliverAsync(Notification notification, CancellationToken ct)
    {
        _lastPlaying.TryGetValue(notification.GuildId, out var previous);
        switch (notification.Kind)
        {
            case NotificationKind.Started:
                if (previous is not null)
                {
                    try
                    {
                        await messages.DeleteAsync(previous.ChannelId, previous.MessageId, ct);
                        _lastPlaying.Remove(notification.GuildId);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning(ex, "Failed to delete the previous playback notification for guild {GuildId}.",
                            notification.GuildId);
                    }
                }
                await SendPlayingAsync(notification, notification.ChannelId!.Value, ct);
                break;

            case NotificationKind.Updated:
                if (previous is null || previous.Item.Id != notification.Player!.CurrentQueueItem!.Id)
                    return;
                _lastPlaying[notification.GuildId] = previous with
                {
                    Player = notification.Player!,
                    Position = notification.Position,
                };
                try
                {
                    await messages.EditAsync(previous.ChannelId, previous.MessageId,
                        PlaybackMessage.Create(notification.Player!, notification.Position, controls: true), ct);
                }
                catch (global::Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    _lastPlaying.Remove(notification.GuildId);
                    await SendPlayingAsync(notification, previous.ChannelId, ct);
                }
                break;

            case NotificationKind.Stopped:
            case NotificationKind.Finished:
            case NotificationKind.Completed:
                if (previous is null)
                    return;
                var finalPosition = notification.Position ?? previous.Position;
                var duration = previous.Item.Track.Duration;
                // Metadata and decoded duration may differ within the display's one-second precision.
                if (notification.Kind == NotificationKind.Completed && duration > TimeSpan.Zero
                    && finalPosition is { } position && position >= TimeSpan.Zero
                    && position >= duration - TimeSpan.FromSeconds(1))
                    finalPosition = duration;
                _lastPlaying[notification.GuildId] = previous with { IsActive = false, Position = finalPosition };
                try
                {
                    await messages.EditAsync(previous.ChannelId, previous.MessageId,
                        PlaybackMessage.Create(previous.Player, finalPosition,
                            title: notification.Kind == NotificationKind.Stopped ? "Playback stopped" : "Playback finished"), ct);
                }
                catch (global::Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    _lastPlaying.Remove(notification.GuildId);
                }
                break;

            case NotificationKind.Failed:
                var item = notification.Item!;
                var card = MusicCard.Create("Song skipped", item.Track,
                        item.RequesterId is { } user ? $"Requested by: <@{user.Value}>" : "Added by: Autoplay")
                    .WithTextDisplay(FindFriendlyError(notification.Error)?.Message ?? "This song couldn't be played.")
                    .WithTextDisplay($"-# {TrackFormatting.Duration(item.Track.Duration)}");
                await messages.SendAsync(notification.ChannelId!.Value, components: MusicCard.Build(card), ct: ct);
                break;
        }
    }

    private async Task SendPlayingAsync(Notification notification, ChannelId channelId, CancellationToken ct)
    {
        var messageId = await messages.SendAsync(channelId,
            ct: ct, components: PlaybackMessage.Create(notification.Player!, notification.Position, controls: true));
        _lastPlaying[notification.GuildId] = new SentMessage(channelId, messageId, notification.Player!, true, notification.Position);
    }

    private static UserFriendlyException? FindFriendlyError(Exception? error)
        => error switch
        {
            UserFriendlyException friendly => friendly,
            AggregateException aggregate => aggregate.InnerExceptions.Select(FindFriendlyError).FirstOrDefault(friendly => friendly is not null),
            _ => error is null ? null : FindFriendlyError(error.InnerException),
        };

    private sealed record Destination(ChannelId ChannelId, ulong InteractionId);
    private sealed record SentMessage(ChannelId ChannelId, ulong MessageId, PlayerSnapshot Player,
        bool IsActive, TimeSpan? Position)
    {
        public QueueItem Item => Player.CurrentQueueItem!;
        public bool IsPaused => Player.IsPaused;
    }
    private enum NotificationKind { Started, Updated, Stopped, Finished, Completed, Failed }
    private sealed record Notification(GuildId GuildId, NotificationKind Kind, ChannelId? ChannelId = null,
        QueueItem? Item = null, Exception? Error = null, TimeSpan? Position = null, PlayerSnapshot? Player = null);
}
