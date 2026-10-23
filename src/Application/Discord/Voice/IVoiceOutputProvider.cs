using Domain.ValueObjects;

namespace Application.Discord.Voice;

public interface IVoiceOutputProvider
{
    IVoiceOutput GetOutput(GuildId guildId);
}
