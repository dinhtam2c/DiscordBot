using Application.Abstractions;
using Domain.ValueObjects;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackQueueProgressor(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork)
{
    public async Task TrackFinishedAsync(GuildId guildId, CancellationToken ct)
    {
        var player = await playerRepository.GetByIdAsync(guildId, ct);
        if (player is null || !player.HasCurrentTrack)
            return;

        player.FinishCurrentTrack();
        await unitOfWork.CommitAsync(ct);
    }
}
