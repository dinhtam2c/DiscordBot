using Application.Abstractions;
using Domain.ValueObjects;
using Modules.Music.Tracks;

namespace Modules.Music.Features.Search;

// Results belong to one user in one guild. Claiming consumes the menu atomically.
internal sealed class SearchResults(IClock clock)
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Result> _results = [];

    public Guid Add(GuildId guildId, UserId userId, IReadOnlyList<Track> tracks)
    {
        lock (_sync)
        {
            var now = clock.UtcNow;
            foreach (var id in _results.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToArray())
                _results.Remove(id);
            var key = Guid.NewGuid();
            _results.Add(key, new Result(guildId, userId, tracks.ToArray(), now.AddMinutes(5)));
            return key;
        }
    }

    public Track Take(Guid id, GuildId guildId, UserId userId, int index)
    {
        lock (_sync)
        {
            if (!_results.TryGetValue(id, out var result) || result.ExpiresAt <= clock.UtcNow)
            {
                _results.Remove(id);
                throw new UserFriendlyException("This selection has expired or was already used. Open the list again.");
            }
            if (result.GuildId != guildId || result.UserId != userId)
                throw new UserFriendlyException("This selection belongs to someone else. Open your own list.");
            if (index < 0 || index >= result.Tracks.Count)
                throw new UserFriendlyException("This selection isn't available. Open the list again.");

            // Do not release a claimed selection on failure: enqueue may already have committed.
            _results.Remove(id);
            return result.Tracks[index];
        }
    }

    private sealed record Result(GuildId GuildId, UserId UserId, IReadOnlyList<Track> Tracks, DateTimeOffset ExpiresAt);
}
