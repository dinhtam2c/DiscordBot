using Modules.Music.Processes.YtDlp;
using Modules.Music.Tracks.AudioSources;

namespace Modules.Music.Tracks.SoundCloud;

internal sealed class SoundCloudAudioSource(YtDlpClient ytdlp) : YtDlpAudioSourceBase(ytdlp)
{
    public override TrackSource Source => TrackSource.SoundCloud;
}
