using System.Threading.Channels;
using Domain.ValueObjects;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackCommandQueue
{
    private readonly Channel<PlaybackCommand> _channel = Channel.CreateUnbounded<PlaybackCommand>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    internal ChannelReader<PlaybackCommand> Reader => _channel.Reader;

    public void EnqueueStart(GuildId guildId, Track track)
        => Write(new StartPlaybackCommand(guildId, track));

    public void EnqueueStop(GuildId guildId)
        => Write(new StopPlaybackCommand(guildId));

    public void EnqueueStopAndLeave(GuildId guildId)
        => Write(new StopAndLeavePlaybackCommand(guildId));

    internal void EnqueueFinished(GuildId guildId, PlaybackSession session, bool shouldAdvanceQueue)
        => Write(new PlaybackFinishedCommand(guildId, session, shouldAdvanceQueue));

    private void Write(PlaybackCommand command)
    {
        if (!_channel.Writer.TryWrite(command))
            throw new InvalidOperationException("The playback command queue is no longer accepting commands.");
    }
}

internal abstract record PlaybackCommand;

internal sealed record StartPlaybackCommand(GuildId GuildId, Track Track) : PlaybackCommand;

internal sealed record StopPlaybackCommand(GuildId GuildId) : PlaybackCommand;

internal sealed record StopAndLeavePlaybackCommand(GuildId GuildId) : PlaybackCommand;

internal sealed record PlaybackFinishedCommand(
    GuildId GuildId,
    PlaybackSession Session,
    bool ShouldAdvanceQueue) : PlaybackCommand;
