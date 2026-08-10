namespace Application.Discord.Runtime;

public interface IDiscordRuntime
{
    ulong BotId { get; }

    string BotName { get; }

    int Latency { get; }

    int GuildCount { get; }

    TimeSpan Uptime { get; }
}
