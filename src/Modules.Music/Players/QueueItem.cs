using Modules.Music.Tracks;
using Domain.ValueObjects;

namespace Modules.Music.Players;

internal sealed record QueueItem(
    Track Track,
    UserId? RequesterId,
    DateTimeOffset RequestedAt)
{
    public bool IsAutoplay => RequesterId is null;

    public Guid Id { get; init; } = Guid.NewGuid();
}
