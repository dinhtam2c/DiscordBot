using System.Threading.Channels;
using Discord;
using Domain.ValueObjects;

namespace Application.Discord.Messages;

public sealed class DiscordMessageQueue
{
    private readonly Channel<DiscordMessage> _messages = Channel.CreateBounded<DiscordMessage>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public Task<ulong> SendAsync(ChannelId channelId, string? text = null, Embed? embed = null,
        CancellationToken ct = default, MessageComponent? components = null)
    {
        if (embed is null)
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return Enqueue(new DiscordMessage.Send(channelId, text, embed, components, ct));
    }

    public Task EditAsync(ChannelId channelId, ulong messageId, Embed embed, MessageComponent components,
        CancellationToken ct = default)
        => Enqueue(new DiscordMessage.Edit(channelId, messageId, embed, components, ct));

    public Task DeleteAsync(ChannelId channelId, ulong messageId, CancellationToken ct = default)
        => Enqueue(new DiscordMessage.Delete(channelId, messageId, ct));

    private Task<ulong> Enqueue(DiscordMessage message)
    {
        if (message.Ct.IsCancellationRequested)
            return Task.FromCanceled<ulong>(message.Ct);

        if (!_messages.Writer.TryWrite(message))
            return Task.FromException<ulong>(new InvalidOperationException("The message queue is full or closed."));

        return message.Completion.Task.WaitAsync(message.Ct);
    }

    public IAsyncEnumerable<DiscordMessage> ReadAllAsync(CancellationToken ct)
        => _messages.Reader.ReadAllAsync(ct);

    public void Complete() => _messages.Writer.TryComplete();
}

public abstract record DiscordMessage(ChannelId ChannelId, CancellationToken Ct)
{
    public TaskCompletionSource<ulong> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public sealed record Send(ChannelId ChannelId, string? Text, Embed? Embed, MessageComponent? Components, CancellationToken Ct)
        : DiscordMessage(ChannelId, Ct);

    public sealed record Edit(ChannelId ChannelId, ulong MessageId, Embed Embed, MessageComponent Components, CancellationToken Ct)
        : DiscordMessage(ChannelId, Ct);

    public sealed record Delete(ChannelId ChannelId, ulong MessageId, CancellationToken Ct)
        : DiscordMessage(ChannelId, Ct);
}
