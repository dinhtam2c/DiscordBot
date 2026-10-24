using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Domain.Players;
using Modules.Music.Infrastructure.Processes.YtDlp;

namespace Modules.Music.Infrastructure.AudioSource;

internal sealed class YoutubeAudioSource(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioSourceBase> logger)
    : YtDlpAudioSourceBase(options, logger)
{
    public override TrackSource Source => TrackSource.Youtube;
}
