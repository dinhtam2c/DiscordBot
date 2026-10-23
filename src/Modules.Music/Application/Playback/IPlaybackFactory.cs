using Application.Discord.Voice;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal interface IPlaybackFactory
{
    IPlaybackInstance Start(Track track, IVoiceOutput voiceOutput, CancellationToken ct);
}
