using Application.Discord.Interactions.Command;
using Discord;
using Discord.WebSocket;
using Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordCommandRegistrar(
    IOptions<DiscordOptions> options,
    DiscordSocketClient client,
    IEnumerable<ICommandDefinition> definitions) : IDiscordReadyHandler
{
    private bool _isRegistered = false;

    public async Task OnReadyAsync(CancellationToken ct)
    {
        if (_isRegistered)
            return;

        var guildId = options.Value.GuildId;

        if (guildId != 0)
        {
            await RegisterGuildCommandsAsync(new GuildId(guildId));
            //await client.GetGlobalApplicationCommandsAsync().ContinueWith(async task =>
            //{
            //    var commands = await task;
            //    foreach (var command in commands)
            //    {
            //        await command.DeleteAsync();
            //    }
            //}, ct);
        }
        //else
        {
            await RegisterGlobalCommandsAsync();
        }

        _isRegistered = true;
    }

    private async Task RegisterGuildCommandsAsync(GuildId guildId)
    {
        var guild = client.GetGuild(guildId.Value);

        foreach (var command in definitions.Select(MapCommand).Where(c => c != null))
        {
            await guild.CreateApplicationCommandAsync(command);
        }
    }

    private async Task RegisterGlobalCommandsAsync()
    {
        foreach (var command in definitions.Select(MapCommand).Where(c => c != null))
        {
            await client.CreateGlobalApplicationCommandAsync(command);
        }
    }

    private SlashCommandProperties? MapCommand(ICommandDefinition definition)
    {
        var commandBuilder = new SlashCommandBuilder()
            .WithName(definition.Name)
            .WithDescription(definition.Description);
        foreach (var option in definition.Options)
        {
            var type = option.Type switch
            {
                CommandOptionType.String => ApplicationCommandOptionType.String,
                CommandOptionType.Integer => ApplicationCommandOptionType.Integer,
                CommandOptionType.Boolean => ApplicationCommandOptionType.Boolean,
                CommandOptionType.Number => ApplicationCommandOptionType.Number,
                CommandOptionType.User => ApplicationCommandOptionType.User,
                CommandOptionType.Channel => ApplicationCommandOptionType.Channel,
                CommandOptionType.Role => ApplicationCommandOptionType.Role,
                _ => throw new ArgumentOutOfRangeException(nameof(option.Type), option.Type, null)
            };
            commandBuilder.AddOption(option.Name, type, option.Description,
                isRequired: option.Required,
                choices: option.Choices?.Select(c => new ApplicationCommandOptionChoiceProperties
                {
                    Name = c.Name,
                    Value = c.Value
                }).ToArray());
        }
        return commandBuilder.Build();
    }
}
