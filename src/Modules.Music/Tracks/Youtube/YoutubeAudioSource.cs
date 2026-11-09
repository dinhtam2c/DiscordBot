using Modules.Music.Processes.YtDlp;
using Modules.Music.Tracks.AudioSources;

namespace Modules.Music.Tracks.Youtube;

internal sealed class YoutubeAudioSource(YtDlpClient ytdlp) : YtDlpAudioSourceBase(ytdlp)
{
    public override TrackSource Source => TrackSource.Youtube;
}
