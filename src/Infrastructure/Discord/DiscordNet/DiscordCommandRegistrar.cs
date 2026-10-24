using Application.Discord.Interactions.Command;
using Discord;
using Discord.WebSocket;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordCommandRegistrar(
    IOptions<DiscordOptions> options,
    DiscordSocketClient client,
    IEnumerable<ICommandDefinition> definitions,
    IEnumerable<IInteractionCommandSource> commandSources,
    ILogger<DiscordCommandRegistrar> logger) : IDiscordReadyHandler
{
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    private bool _isRegistered;

    public async Task OnReadyAsync(CancellationToken ct)
    {
        await _registrationLock.WaitAsync(ct);
        try
        {
            if (_isRegistered)
                return;

            var desiredDefinitions = definitions
                .Concat(commandSources.SelectMany(source => source.Commands))
                .ToArray();

            var duplicateName = desiredDefinitions
                .GroupBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateName is not null)
                throw new InvalidOperationException($"More than one application command is named '{duplicateName.Key}'.");

            var desiredCommands = desiredDefinitions
                .Select(MapCommand)
                .Cast<ApplicationCommandProperties>()
                .ToArray();

            if (options.Value.GuildId != 0)
                await ReconcileGuildCommandsAsync(new GuildId(options.Value.GuildId), desiredDefinitions, desiredCommands);
            else
                await ReconcileGlobalCommandsAsync(desiredDefinitions, desiredCommands);

            _isRegistered = true;
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    private async Task ReconcileGuildCommandsAsync(
        GuildId guildId,
        IReadOnlyCollection<ICommandDefinition> desiredDefinitions,
        ApplicationCommandProperties[] desiredCommands)
    {
        var guild = client.GetGuild(guildId.Value)
            ?? throw new InvalidOperationException($"Guild {guildId} is not available in the Discord cache.");
        var existingCommands = await guild.GetApplicationCommandsAsync();

        if (!HasChanges(existingCommands, desiredDefinitions))
        {
            logger.LogInformation("Guild application commands are already current for guild {GuildId}.", guildId);
            return;
        }

        await guild.BulkOverwriteApplicationCommandAsync(desiredCommands);
        logger.LogInformation("Reconciled {CommandCount} guild application commands for guild {GuildId}.", desiredCommands.Length, guildId);
    }

    private async Task ReconcileGlobalCommandsAsync(
        IReadOnlyCollection<ICommandDefinition> desiredDefinitions,
        ApplicationCommandProperties[] desiredCommands)
    {
        var existingCommands = await client.GetGlobalApplicationCommandsAsync();

        if (!HasChanges(existingCommands, desiredDefinitions))
        {
            logger.LogInformation("Global application commands are already current.");
            return;
        }

        await client.BulkOverwriteGlobalApplicationCommandsAsync(desiredCommands);
        logger.LogInformation("Reconciled {CommandCount} global application commands.", desiredCommands.Length);
    }

    private static bool HasChanges(
        IReadOnlyCollection<SocketApplicationCommand> existingCommands,
        IReadOnlyCollection<ICommandDefinition> desiredDefinitions)
    {
        if (existingCommands.Count != desiredDefinitions.Count)
            return true;

        var existingByName = existingCommands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var desired in desiredDefinitions)
        {
            if (!existingByName.TryGetValue(desired.Name, out var existing)
                || existing.Type != ApplicationCommandType.Slash
                || !string.Equals(existing.Description, desired.Description, StringComparison.Ordinal)
                || HasOptionChanges(existing.Options, desired.Options))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOptionChanges(
        IReadOnlyCollection<SocketApplicationCommandOption> existingOptions,
        IReadOnlyList<CommandOptionDefinition> desiredOptions)
    {
        if (existingOptions.Count != desiredOptions.Count)
            return true;

        var existing = existingOptions.ToArray();
        for (var index = 0; index < desiredOptions.Count; index++)
        {
            var actualOption = existing[index];
            var desiredOption = desiredOptions[index];
            if (!string.Equals(actualOption.Name, desiredOption.Name, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actualOption.Description, desiredOption.Description, StringComparison.Ordinal)
                || actualOption.Type != MapOptionType(desiredOption.Type)
                || actualOption.IsRequired.GetValueOrDefault() != desiredOption.Required
                || HasChoiceChanges(actualOption.Choices, desiredOption.Choices))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasChoiceChanges(
        IReadOnlyCollection<SocketApplicationCommandChoice> existingChoices,
        IReadOnlyList<CommandOptionChoice>? desiredChoices)
    {
        var expected = desiredChoices ?? [];
        if (existingChoices.Count != expected.Count)
            return true;

        var actual = existingChoices.ToArray();
        for (var index = 0; index < expected.Count; index++)
        {
            if (!string.Equals(actual[index].Name, expected[index].Name, StringComparison.Ordinal)
                || !string.Equals(
                    Convert.ToString(actual[index].Value, System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToString(expected[index].Value, System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static SlashCommandProperties MapCommand(ICommandDefinition definition)
    {
        var commandBuilder = new SlashCommandBuilder()
            .WithName(definition.Name)
            .WithDescription(definition.Description);

        foreach (var option in definition.Options)
        {
            commandBuilder.AddOption(
                option.Name,
                MapOptionType(option.Type),
                option.Description,
                isRequired: option.Required,
                choices: option.Choices?.Select(choice => new ApplicationCommandOptionChoiceProperties
                {
                    Name = choice.Name,
                    Value = choice.Value
                }).ToArray());
        }

        return commandBuilder.Build();
    }

    private static ApplicationCommandOptionType MapOptionType(CommandOptionType type)
        => type switch
        {
            CommandOptionType.String => ApplicationCommandOptionType.String,
            CommandOptionType.Integer => ApplicationCommandOptionType.Integer,
            CommandOptionType.Boolean => ApplicationCommandOptionType.Boolean,
            CommandOptionType.Number => ApplicationCommandOptionType.Number,
            CommandOptionType.User => ApplicationCommandOptionType.User,
            CommandOptionType.Channel => ApplicationCommandOptionType.Channel,
            CommandOptionType.Role => ApplicationCommandOptionType.Role,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
}
