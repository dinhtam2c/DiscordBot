using Domain.ValueObjects;

namespace Modules.Music.Domain.Players;

public record QueueItem(
    Track Track,
    UserId RequesterId,
    DateTimeOffset RequestedAt);
