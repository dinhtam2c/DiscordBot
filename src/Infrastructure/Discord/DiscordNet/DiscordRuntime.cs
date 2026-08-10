using Application.Abstractions;
using Application.Discord.Runtime;
using Discord.WebSocket;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordRuntime(DiscordSocketClient client, IClock clock) : IDiscordRuntime
{
    public ulong BotId => client.CurrentUser.Id;

    public string BotName => client.CurrentUser.Username;

    public int Latency => client.Latency;

    public int GuildCount => client.Guilds.Count;

    public TimeSpan Uptime => clock.Uptime;
}
