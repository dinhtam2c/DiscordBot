using Domain.Core;
using Domain.ValueObjects;

namespace Modules.Music.Domain.Players.Events;

internal record PlaybackStartedEvent(GuildId GuildId, Track Track, Volume Volume) : IDomainEvent;
