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

    public void Playing(GuildId guildId, QueueItem item)
        => Publish(guildId, () => CreateEmbed("Now playing", item).Build(), replacePlaying: true);

    public void Failed(GuildId guildId, QueueItem item, Exception? error)
        => Publish(guildId, () => CreateEmbed("Song skipped", item)
            .WithDescription($"**{Format.Url(TrackFormatting.EscapeText(item.Track.Title), item.Track.SourceUrl)}**\n"
                + (FindFriendlyError(error)?.Message ?? "This song couldn't be played."))
            .Build());

    private void Publish(GuildId guildId, Func<Embed> createEmbed, bool replacePlaying = false)
    {
        if (!_destinations.TryGetValue(guildId, out var destination))
            return;

        try
        {
            if (!_pending.Writer.TryWrite(new Notification(guildId, destination.ChannelId, createEmbed(), replacePlaying)))
                logger.LogWarning("Failed to queue a music notification for guild {GuildId}, channel {ChannelId}. The message queue is full or closed.",
                    guildId, destination.ChannelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create a music notification for guild {GuildId}.", guildId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var notification in _pending.Reader.ReadAllAsync(ct))
            {
                try
                {
                    if (notification.ReplacePlaying && _lastPlaying.TryGetValue(notification.GuildId, out var previous))
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

                    var messageId = await messages.SendAsync(notification.ChannelId, embed: notification.Embed, ct: ct);
                    if (notification.ReplacePlaying)
                        _lastPlaying[notification.GuildId] = new SentMessage(notification.ChannelId, messageId);
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
    private sealed record SentMessage(ChannelId ChannelId, ulong MessageId);
    private sealed record Notification(GuildId GuildId, ChannelId ChannelId, Embed Embed, bool ReplacePlaying);
}
