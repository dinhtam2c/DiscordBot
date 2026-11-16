using Application.Discord.Voice;
using Microsoft.Extensions.Hosting;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Voice;

internal sealed class VoicePresenceObserver(IVoiceChannelPresence presence, PlayerCoordinator players) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        presence.Changed += players.RefreshVoicePresence;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        presence.Changed -= players.RefreshVoicePresence;
        return Task.CompletedTask;
    }
}
