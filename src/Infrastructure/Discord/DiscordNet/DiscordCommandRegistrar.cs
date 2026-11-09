using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordCommandRegistrar(
    IOptions<DiscordOptions> options,
    IOptions<DiscordCommandOptions> commandOptions,
    DiscordSocketClient client,
    InteractionService commands,
    IServiceScopeFactory scopes,
    ILogger<DiscordCommandRegistrar> logger)
{
    public async Task LoadModulesAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        foreach (var module in commandOptions.Value.Modules.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            await commands.AddModuleAsync(module, services);
        }
        foreach (var configure in commandOptions.Value.ConfigureModules)
        {
            ct.ThrowIfCancellationRequested();
            await configure(commands, services);
        }

        var duplicate = commands.SlashCommands.GroupBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Application command '{duplicate.Key}' is registered more than once.");
    }

    public async Task SynchronizeAsync(CancellationToken ct)
    {
        var desired = commands.SlashCommands.ToArray();
        var requestOptions = new RequestOptions { CancelToken = ct };
        if (options.Value.GuildId != 0)
        {
            var guild = client.GetGuild(options.Value.GuildId)
                ?? throw new InvalidOperationException($"Guild {options.Value.GuildId} is not available in the Discord cache.");
            var existing = await guild.GetApplicationCommandsAsync(options: requestOptions);
            if (HasChanges(existing, desired))
            {
                ct.ThrowIfCancellationRequested();
                await commands.RegisterCommandsToGuildAsync(guild.Id);
                logger.LogInformation("Updated {CommandCount} application commands for guild {GuildId}.", desired.Length, guild.Id);
            }
        }
        else
        {
            var existing = await client.GetGlobalApplicationCommandsAsync(options: requestOptions);
            if (HasChanges(existing, desired, global: true))
            {
                ct.ThrowIfCancellationRequested();
                await commands.RegisterCommandsGloballyAsync();
                logger.LogInformation("Updated {CommandCount} global application commands.", desired.Length);
            }
        }
    }

    private static bool HasChanges(IReadOnlyCollection<SocketApplicationCommand> existing, SlashCommandInfo[] desired, bool global = false)
    {
        if (existing.Count != desired.Length)
            return true;
        var existingByName = existing.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        return desired.Any(command => !existingByName.TryGetValue(command.Name, out var actual)
            || actual.Type != ApplicationCommandType.Slash
            || actual.Description != command.Description
            || actual.IsNsfw != command.IsNsfw
            || actual.DefaultMemberPermissions.RawValue != (ulong)(command.DefaultMemberPermissions ?? 0)
            || (global && command.ContextTypes is { Count: > 0 } contexts && !contexts.ToHashSet().SetEquals(actual.ContextTypes ?? []))
            || (global && command.IntegrationTypes is { Count: > 0 } integrations && !integrations.ToHashSet().SetEquals(actual.IntegrationTypes ?? []))
            || HasOptionChanges(actual.Options ?? [], command.FlattenedParameters));
    }

    private static bool HasOptionChanges(
        IReadOnlyCollection<SocketApplicationCommandOption> actual,
        IReadOnlyCollection<SlashCommandParameterInfo> desired)
    {
        if (actual.Count != desired.Count)
            return true;
        return actual.Zip(desired).Any(pair =>
            pair.First.Name != pair.Second.Name
            || pair.First.Description != pair.Second.Description
            || pair.First.Type != (pair.Second.DiscordOptionType ?? pair.Second.TypeConverter.GetDiscordType())
            || pair.First.IsRequired.GetValueOrDefault() != pair.Second.IsRequired
            || pair.First.IsAutocomplete.GetValueOrDefault() != pair.Second.IsAutocomplete
            || HasNumericRangeChanges(pair.First, pair.Second)
            || pair.First.MinLength != pair.Second.MinLength
            || pair.First.MaxLength != pair.Second.MaxLength
            || !(pair.Second.ChannelTypes ?? []).ToHashSet().SetEquals(pair.First.ChannelTypes ?? [])
            || HasChoiceChanges(pair.First.Choices ?? [], pair.Second.Choices ?? [])
            || pair.First.Options is { Count: > 0 });
    }

    private static bool HasNumericRangeChanges(SocketApplicationCommandOption actual, SlashCommandParameterInfo desired)
    {
        if (actual.Type is not (ApplicationCommandOptionType.Integer or ApplicationCommandOptionType.Number))
            return false;

        // Discord.Net fills these defaults in metadata, while Discord may omit unrestricted bounds.
        const double maximum = 9007199254740991;
        return actual.MinValue.GetValueOrDefault(-maximum) != desired.MinValue.GetValueOrDefault(-maximum)
            || actual.MaxValue.GetValueOrDefault(maximum) != desired.MaxValue.GetValueOrDefault(maximum);
    }

    private static bool HasChoiceChanges(
        IReadOnlyCollection<SocketApplicationCommandChoice> actual,
        IReadOnlyCollection<ParameterChoice> desired)
        => actual.Count != desired.Count || actual.Zip(desired).Any(pair =>
            pair.First.Name != pair.Second.Name
            || !string.Equals(Convert.ToString(pair.First.Value, System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToString(pair.Second.Value, System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
}
