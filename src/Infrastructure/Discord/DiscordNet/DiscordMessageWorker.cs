using Application.Discord.Messages;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordMessageWorker(
    DiscordMessageQueue messages,
    DiscordSocketClient client,
    ILogger<DiscordMessageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in messages.ReadAllAsync(ct))
            {
                using var delivery = CancellationTokenSource.CreateLinkedTokenSource(ct, message.Ct);
                delivery.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    var options = new RequestOptions { CancelToken = delivery.Token };
                    delivery.Token.ThrowIfCancellationRequested();
                    var channel = client.GetChannel(message.ChannelId.Value) as IMessageChannel
                        ?? await client.Rest.GetChannelAsync(message.ChannelId.Value, options) as IMessageChannel
                        ?? throw new InvalidOperationException($"Channel {message.ChannelId} is not available for messaging.");

                    switch (message)
                    {
                        case DiscordMessage.Send send:
                            var sent = await channel.SendMessageAsync(
                                allowedMentions: AllowedMentions.None, components: send.Components, options: options,
                                flags: MessageFlags.ComponentsV2);
                            message.Completion.TrySetResult(sent.Id);
                            break;
                        case DiscordMessage.Edit edit:
                            await channel.ModifyMessageAsync(edit.MessageId, properties =>
                                DiscordMessageContent.Apply(properties, components: edit.Components), options);
                            message.Completion.TrySetResult(edit.MessageId);
                            break;
                        case DiscordMessage.Delete delete:
                            await channel.DeleteMessageAsync(delete.MessageId, options);
                            message.Completion.TrySetResult(0);
                            break;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    message.Completion.TrySetCanceled(ct);
                    break;
                }
                catch (OperationCanceledException) when (message.Ct.IsCancellationRequested)
                {
                    message.Completion.TrySetCanceled(message.Ct);
                }
                catch (Exception ex)
                    when (message is DiscordMessage.Delete && ex is global::Discord.Net.HttpException { DiscordCode: DiscordErrorCode.UnknownMessage })
                {
                    message.Completion.TrySetResult(0);
                }
                catch (Exception ex)
                {
                    message.Completion.TrySetException(ex);
                    logger.LogError(ex, "Failed to process a message operation for channel {ChannelId}.", message.ChannelId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            messages.Complete();
            await foreach (var pending in messages.ReadAllAsync(CancellationToken.None))
                pending.Completion.TrySetCanceled(new CancellationToken(true));
        }
    }

    public override Task StopAsync(CancellationToken ct)
    {
        messages.Complete();
        return base.StopAsync(ct);
    }
}
