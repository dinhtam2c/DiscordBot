using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;

namespace Modules.Utility;

internal sealed class UtilityCommands(
    InteractionService commands,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("help", "Display help information.")]
    public Task HelpAsync()
    {
        var lines = commands.SlashCommands.OrderBy(command => command.Name)
            .Select(command => $"- `{command.Name}`: {command.Description}");
        return CompleteAsync("Available commands:\n" + string.Join('\n', lines));
    }

    [SlashCommand("about", "Get information about the bot.")]
    public Task AboutAsync() => CompleteAsync($"Bot ngu: {Context.Client.CurrentUser.Username}\n");

    [SlashCommand("ping", "Ping the bot.")]
    public Task PingAsync() => CompleteAsync($"Pong!\nLatency: {Context.Client.Latency} ms");

    [SlashCommand("echo", "Echoes the input message.")]
    public Task EchoAsync([Summary(description: "The message to echo.")] string message)
        => CompleteAsync(message);

    [SlashCommand("userinfo", "Get information about a user.")]
    [CommandContextType(InteractionContextType.Guild)]
    public Task UserInfoAsync([Summary(description: "The user to get information about.")] IUser? user = null)
    {
        var selected = user ?? Context.User;
        return CompleteAsync(embed: new EmbedBuilder()
            .WithTitle("User Information")
            .WithThumbnailUrl(selected.GetDisplayAvatarUrl())
            .AddField("Username", selected.Username, true)
            .AddField("Display Name", selected is IGuildUser guildUser ? guildUser.DisplayName : selected.GlobalName ?? selected.Username, true)
            .Build());
    }
}
