using Domain.ValueObjects;

namespace Application.Discord.Voice;

public interface IVoiceChannelPresence
{
    event Action<GuildId>? Changed;

    // Null means the gateway has not supplied a reliable snapshot.
    bool? HasListeners(GuildId guildId, ChannelId channelId);
}
