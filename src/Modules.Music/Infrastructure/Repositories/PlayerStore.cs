using Domain.ValueObjects;
using Modules.Music.Domain.Players;
using System.Collections.Concurrent;

namespace Modules.Music.Infrastructure.Repositories;

internal class PlayerStore
{
    private readonly ConcurrentDictionary<GuildId, Player> _players = new();

    public bool TryAdd(GuildId guildId, Player player) => _players.TryAdd(guildId, player);

    public bool TryGetValue(GuildId guildId, out Player? player) => _players.TryGetValue(guildId, out player);

    public bool TryRemove(GuildId guildId, out Player? player) => _players.TryRemove(guildId, out player);
}
