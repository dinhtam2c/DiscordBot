using Application.Abstractions;
using Application.Discord.Interactions;
using Discord.Interactions;

namespace Modules.Utility;

internal sealed class UtilityCommands(
    InteractionService commands,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("help", "See all commands and how to use them.")]
    public Task HelpAsync()
    {
        var lines = commands.SlashCommands.OrderBy(command => command.Name)
            .Select(command => $"- `/{command.Name}` — {command.Description}");
        return CompleteAsync("**Available commands**\n" + string.Join('\n', lines));
    }

    [SlashCommand("ping", "Check the bot's connection to Discord.")]
    public Task PingAsync() => CompleteAsync($"Discord latency: **{Context.Client.Latency} ms**.");
}
