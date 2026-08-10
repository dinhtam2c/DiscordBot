using Application.Discord.Voice;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal interface IPlaybackFactory
{
    Task<IPlaybackInstance> StartAsync(Track track, IVoiceSession voiceSession, CancellationToken ct);
}
