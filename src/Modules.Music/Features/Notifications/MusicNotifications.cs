using System.Collections.Concurrent;
using System.Threading.Channels;
using Application.Abstractions;
using Application.Discord.Messages;
using Discord;
using Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Music.Players;

namespace Modules.Music.Features.Notifications;

internal sealed class MusicNotifications(DiscordMessageQueue messages, ILogger<MusicNotifications> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<GuildId, Destination> _destinations = new();
    private readonly Dictionary<GuildId, SentMessage> _lastPlaying = new();
    private readonly Channel<Notification> _pending = Channel.CreateBounded<Notification>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public void SetChannel(GuildId guildId, ChannelId channelId, ulong interactionId)
    {
        var destination = new Destination(channelId, interactionId);
        // An older interaction may reach its handler after a newer one has already run.
        _destinations.AddOrUpdate(guildId, destination,
            (_, current) => interactionId > current.InteractionId ? destination : current);
    }

    public void Playing(GuildId guildId, QueueItem item, bool isPaused = false)
        => PublishToChannel(guildId, NotificationKind.Started, item, isPaused);

    public void Updated(GuildId guildId, QueueItem item, bool isPaused)
        => Publish(new Notification(guildId, NotificationKind.Updated, Item: item, IsPaused: isPaused));

    public void Ended(GuildId guildId, bool stopped)
        => Publish(new Notification(guildId, stopped ? NotificationKind.Stopped : NotificationKind.Finished));

    public void Failed(GuildId guildId, QueueItem item, Exception? error)
        => PublishToChannel(guildId, NotificationKind.Failed, item, error: error);

    private void PublishToChannel(GuildId guildId, NotificationKind kind, QueueItem item, bool isPaused = false, Exception? error = null)
    {
        if (!_destinations.TryGetValue(guildId, out var destination))
            return;

        Publish(new Notification(guildId, kind, destination.ChannelId, item, isPaused, error));
    }

    private void Publish(Notification notification)
    {
        if (!_pending.Writer.TryWrite(notification))
            logger.LogWarning("Failed to queue a music notification for guild {GuildId}. The notification queue is full or closed.",
                notification.GuildId);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var notification in _pending.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await DeliverAsync(notification, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish a music notification for guild {GuildId}.", notification.GuildId);
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
                if (previous is null || previous.Item.Id != notification.Item!.Id)
                    return;
                try
                {
                    await messages.EditAsync(previous.ChannelId, previous.MessageId,
                        CreateEmbed(notification.IsPaused ? "Paused" : "Now playing", notification.Item).Build(),
                        CreateControls(notification.Item, notification.IsPaused), ct);
                }
                catch (global::Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    _lastPlaying.Remove(notification.GuildId);
                    await SendPlayingAsync(notification, previous.ChannelId, ct);
                }
                break;

            case NotificationKind.Stopped:
            case NotificationKind.Finished:
                if (previous is null)
                    return;
                try
                {
                    await messages.EditAsync(previous.ChannelId, previous.MessageId,
                        CreateEmbed(notification.Kind == NotificationKind.Stopped ? "Playback stopped" : "Playback finished", previous.Item).Build(),
                        new ComponentBuilder().Build(), ct);
                }
                catch (global::Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    _lastPlaying.Remove(notification.GuildId);
                }
                break;

            case NotificationKind.Failed:
                var item = notification.Item!;
                await messages.SendAsync(notification.ChannelId!.Value, embed: CreateEmbed("Song skipped", item)
                    .WithDescription($"**{Format.Url(TrackFormatting.EscapeText(item.Track.Title), item.Track.SourceUrl)}**\n"
                        + (FindFriendlyError(notification.Error)?.Message ?? "This song couldn't be played."))
                    .Build(), ct: ct);
                break;
        }
    }

    private async Task SendPlayingAsync(Notification notification, ChannelId channelId, CancellationToken ct)
    {
        var item = notification.Item!;
        var messageId = await messages.SendAsync(channelId,
            embed: CreateEmbed(notification.IsPaused ? "Paused" : "Now playing", item).Build(),
            ct: ct, components: CreateControls(item, notification.IsPaused));
        _lastPlaying[notification.GuildId] = new SentMessage(channelId, messageId, item);
    }

    private static MessageComponent CreateControls(QueueItem item, bool isPaused)
        => new ComponentBuilder()
            .WithButton(isPaused ? "Resume" : "Pause", $"music:{(isPaused ? "resume" : "pause")}:{item.Id:N}",
                ButtonStyle.Secondary, emote: new Emoji(isPaused ? "\u25B6\uFE0F" : "\u23F8\uFE0F"))
            .WithButton("Skip", $"music:skip:{item.Id:N}", ButtonStyle.Primary, emote: new Emoji("\u23ED\uFE0F"))
            .WithButton("Stop", $"music:stop:{item.Id:N}", ButtonStyle.Danger, emote: new Emoji("\u23F9\uFE0F"))
            .Build();

    private static EmbedBuilder CreateEmbed(string title, QueueItem item)
        => new EmbedBuilder()
            .WithTitle(title)
            .WithDescription($"**{Format.Url(TrackFormatting.EscapeText(item.Track.Title), item.Track.SourceUrl)}**")
            .WithThumbnailUrl(item.Track.ThumbnailUrl)
            .AddField("Artist", TrackFormatting.Artist(item.Track), true)
            .AddField("Duration", TrackFormatting.Duration(item.Track.Duration), true)
            .AddField("Requested by", $"<@{item.RequesterId.Value}>", true);

    private static UserFriendlyException? FindFriendlyError(Exception? error)
        => error switch
        {
            UserFriendlyException friendly => friendly,
            AggregateException aggregate => aggregate.InnerExceptions.Select(FindFriendlyError).FirstOrDefault(friendly => friendly is not null),
            _ => error is null ? null : FindFriendlyError(error.InnerException),
        };

    private sealed record Destination(ChannelId ChannelId, ulong InteractionId);
    private sealed record SentMessage(ChannelId ChannelId, ulong MessageId, QueueItem Item);
    private enum NotificationKind { Started, Updated, Stopped, Finished, Failed }
    private sealed record Notification(GuildId GuildId, NotificationKind Kind, ChannelId? ChannelId = null,
        QueueItem? Item = null, bool IsPaused = false, Exception? Error = null);
}
