using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Domain.Players;
using Modules.Music.Infrastructure.Processes.YtDlp;

namespace Modules.Music.Infrastructure.AudioSource.SoundCloud;

internal sealed class SoundCloudAudioSource(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioSourceBase> logger)
    : YtDlpAudioSourceBase(options, logger)
{
    public override TrackSource Source => TrackSource.SoundCloud;
}
