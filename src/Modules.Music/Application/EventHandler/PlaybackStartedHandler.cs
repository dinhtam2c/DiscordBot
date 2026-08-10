using Application.Discord.Voice;
using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlaybackStartedHandler(
    IVoiceSessionManager voiceSessionManager,
    PlaybackController playbackController) : IEventHandler<PlaybackStartedEvent>
{
    public async Task HandleAsync(PlaybackStartedEvent @event, CancellationToken ct)
    {
        var voiceSession = voiceSessionManager.GetSession(@event.Player.GuildId);
        if (voiceSession is null)
        {
            throw new InvalidOperationException("Voice session not found for the specified guild.");
        }

        await playbackController.StartAsync(@event.Player, voiceSession);
    }
}
