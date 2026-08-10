using Domain.ValueObjects;
using Infrastructure.Persistence;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players;

namespace Modules.Music.Infrastructure.Repositories;

internal sealed class MemoryPlayerRepository(PlayerStore store, MemoryPersistenceContext context) : IPlayerRepository
{
    public Task AddAsync(Player player, CancellationToken ct)
    {
        if (!store.TryAdd(player.GuildId, player))
        {
            throw new InvalidOperationException($"Player for guild {player.GuildId} already exists.");
        }

        context.Tracker.Track(player);
        return Task.CompletedTask;
    }

    public Task<Player?> GetByIdAsync(GuildId guildId, CancellationToken ct)
    {
        store.TryGetValue(guildId, out var player);
        if (player is not null)
        {
            context.Tracker.Track(player);
        }

        return Task.FromResult(player);
    }

    public Task RemoveAsync(GuildId guildId, CancellationToken ct)
    {
        store.TryRemove(guildId, out _);
        return Task.CompletedTask;
    }
}
