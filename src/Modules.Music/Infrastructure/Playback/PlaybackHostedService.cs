using Microsoft.Extensions.Hosting;
using Modules.Music.Application.Playback;

namespace Modules.Music.Infrastructure.Playback;

internal sealed class PlaybackHostedService(PlaybackWorker playbackWorker) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => playbackWorker.RunAsync(stoppingToken);
}
