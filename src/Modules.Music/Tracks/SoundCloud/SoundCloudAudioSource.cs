using Modules.Music.Tracks.AudioSources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Processes.YtDlp;

namespace Modules.Music.Tracks.SoundCloud;

internal sealed class SoundCloudAudioSource(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioSourceBase> logger)
    : YtDlpAudioSourceBase(options, logger)
{
    public override TrackSource Source => TrackSource.SoundCloud;
}
