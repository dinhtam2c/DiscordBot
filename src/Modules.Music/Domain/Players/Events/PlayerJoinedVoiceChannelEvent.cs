using Domain.Core;
using Domain.ValueObjects;

namespace Modules.Music.Domain.Players.Events;

internal record PlayerJoinedVoiceChannelEvent(GuildId GuildId, ChannelId VoiceChannelId) : IDomainEvent;
