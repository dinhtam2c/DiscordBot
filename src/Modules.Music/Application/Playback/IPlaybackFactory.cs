using Application.Discord.Voice;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal interface IPlaybackFactory
{
    IPlaybackInstance Start(Track track, Volume volume, IVoiceOutput voiceOutput, CancellationToken ct);
}
