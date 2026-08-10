using Domain.ValueObjects;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Repositories;

internal interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(GuildId guildId, CancellationToken ct);

    Task AddAsync(Player player, CancellationToken ct);

    Task RemoveAsync(GuildId guildId, CancellationToken ct);
}
