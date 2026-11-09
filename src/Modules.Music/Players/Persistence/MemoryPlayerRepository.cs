using Domain.ValueObjects;
using Infrastructure.Persistence;

namespace Modules.Music.Players.Persistence;

internal sealed class MemoryPlayerRepository(
    InMemoryStore<GuildId, Player> store,
    MemoryPersistenceContext context)
    : InMemoryRepository<GuildId, Player>(store, context), IPlayerRepository
{
    protected override GuildId GetKey(Player aggregate) => aggregate.GuildId;

    protected override Player Copy(Player aggregate) => aggregate.CreateWorkingCopy();

    public Task AddAsync(Player player, CancellationToken ct) => StageAddAsync(player, ct);

    public Task<Player?> GetByIdAsync(GuildId guildId, CancellationToken ct) => GetWorkingCopyAsync(guildId, ct);

    public Task RemoveAsync(GuildId guildId, CancellationToken ct) => StageRemoveAsync(guildId, ct);
}
