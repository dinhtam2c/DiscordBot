using Domain.Core;
using Domain.ValueObjects;

namespace Modules.Music.Domain.Players.Events;

internal sealed record PlayerVolumeChangedEvent(GuildId GuildId, Volume Volume) : IDomainEvent;
