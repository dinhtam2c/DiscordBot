using Modules.Music.Tracks.AudioSources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Processes.YtDlp;

namespace Modules.Music.Tracks.Youtube;

internal sealed class YoutubeAudioSource(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioSourceBase> logger)
    : YtDlpAudioSourceBase(options, logger)
{
    public override TrackSource Source => TrackSource.Youtube;
}
