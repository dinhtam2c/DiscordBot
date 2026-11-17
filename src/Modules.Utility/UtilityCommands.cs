using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Messages;
using Discord.Interactions;
using Discord;

namespace Modules.Utility;

internal sealed class UtilityCommands(
    InteractionService commands,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("help", "See all commands and how to use them.")]
    [Ephemeral]
    public Task HelpAsync()
    {
        var lines = commands.SlashCommands.OrderBy(command => command.Name)
            .Select(command => $"- `/{command.Name}` — {MessageFormatting.Text(command.Description)}");
        return CompleteAsync(components: new ComponentBuilderV2().WithContainer(new ContainerBuilder()
            .WithTextDisplay("### Available commands\n" + string.Join('\n', lines))).Build());
    }

    [SlashCommand("ping", "Check the bot's connection to Discord.")]
    [Ephemeral]
    public Task PingAsync() => CompleteAsync($"Discord latency: **{Context.Client.Latency} ms**.");
}
