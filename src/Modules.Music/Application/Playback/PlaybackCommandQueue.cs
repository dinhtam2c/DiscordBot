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

    public void EnqueuePause(GuildId guildId)
        => Write(new PausePlaybackCommand(guildId));

    public void EnqueueResume(GuildId guildId)
        => Write(new ResumePlaybackCommand(guildId));

    public async Task StopAsync(GuildId guildId, CancellationToken ct)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Write(new StopPlaybackCommand(guildId, completion));
        await completion.Task.WaitAsync(ct);
    }

    internal void EnqueueFinished(GuildId guildId, IPlaybackInstance playback, bool shouldAdvanceQueue)
        => Write(new PlaybackFinishedCommand(guildId, playback, shouldAdvanceQueue));

    private void Write(PlaybackCommand command)
    {
        if (!_channel.Writer.TryWrite(command))
            throw new InvalidOperationException("The playback command queue is no longer accepting commands.");
    }
}

internal abstract record PlaybackCommand;

internal sealed record StartPlaybackCommand(GuildId GuildId, Track Track) : PlaybackCommand;

internal sealed record StopPlaybackCommand(
    GuildId GuildId,
    TaskCompletionSource? Completion = null) : PlaybackCommand;

internal sealed record PausePlaybackCommand(GuildId GuildId) : PlaybackCommand;

internal sealed record ResumePlaybackCommand(GuildId GuildId) : PlaybackCommand;

internal sealed record PlaybackFinishedCommand(
    GuildId GuildId,
    IPlaybackInstance Playback,
    bool ShouldAdvanceQueue) : PlaybackCommand;
