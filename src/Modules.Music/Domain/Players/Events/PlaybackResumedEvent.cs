using Domain.Core;
using Domain.ValueObjects;

namespace Modules.Music.Domain.Players.Events;

internal sealed record PlaybackResumedEvent(GuildId GuildId) : IDomainEvent;
