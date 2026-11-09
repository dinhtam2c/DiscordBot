using Modules.Music.Tracks;
using Application.Discord.Voice;
using Modules.Music.Players;

namespace Modules.Music.Playback;

internal interface IPlaybackFactory
{
    IPlaybackInstance Start(Track track, Volume volume, IVoiceOutput voiceOutput, CancellationToken ct);
}
