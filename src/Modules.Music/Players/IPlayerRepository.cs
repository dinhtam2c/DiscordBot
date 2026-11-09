using Domain.ValueObjects;

namespace Modules.Music.Players;

internal interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(GuildId guildId, CancellationToken ct);

    Task AddAsync(Player player, CancellationToken ct);

    Task RemoveAsync(GuildId guildId, CancellationToken ct);
}
