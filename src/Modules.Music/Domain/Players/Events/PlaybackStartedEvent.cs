using Domain.Core;

namespace Modules.Music.Domain.Players.Events;

internal record PlaybackStartedEvent(Player Player) : IDomainEvent;
