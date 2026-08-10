using Domain.ValueObjects;

namespace Modules.Music.Domain.Players;

internal sealed record QueueItem(
    Track Track,
    UserId RequesterId,
    DateTimeOffset RequestedAt);
