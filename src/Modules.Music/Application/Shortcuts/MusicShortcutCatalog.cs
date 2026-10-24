using Application.Discord.Interactions.Command;
using Application.Discord.Interactions.Dispatcher;
using Microsoft.Extensions.Options;
using Modules.Music.Application.UseCases.EnqueueTracks;
using System.Text.RegularExpressions;

namespace Modules.Music.Application.Shortcuts;

internal sealed class MusicShortcutOptions
{
    public const string SectionName = "Music";

    public Dictionary<string, MusicShortcutSettings> Shortcuts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class MusicShortcutSettings
{
    public string Description { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public EnqueueTracksMode Mode { get; set; } = EnqueueTracksMode.Next;
}

internal sealed record MusicShortcut(
    string Name,
    string Description,
    string Query,
    EnqueueTracksMode Mode);

internal sealed class MusicShortcutCatalog : IInteractionCommandSource
{
    private static readonly Regex ValidName = new("^[a-z0-9_-]{1,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly Dictionary<string, MusicShortcut> _shortcuts;

    public IReadOnlyCollection<ICommandDefinition> Commands { get; }

    public IReadOnlyCollection<IInteractionRegistration> Registrations { get; }

    public MusicShortcutCatalog(
        IOptions<MusicShortcutOptions> options,
        IEnumerable<ICommandDefinition> commandDefinitions,
        IEnumerable<IInteractionRegistration> registrations)
    {
        var reservedNames = commandDefinitions.Select(command => command.Name)
            .Concat(registrations.Select(registration => registration.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _shortcuts = new Dictionary<string, MusicShortcut>(StringComparer.OrdinalIgnoreCase);
        foreach (var (configuredName, settings) in options.Value.Shortcuts)
        {
            var name = configuredName.Trim().ToLowerInvariant();
            if (!ValidName.IsMatch(name))
                throw new InvalidOperationException($"Music shortcut name '{configuredName}' is invalid.");

            if (reservedNames.Contains(name))
                throw new InvalidOperationException($"Music shortcut '{name}' conflicts with an existing command.");

            if (string.IsNullOrWhiteSpace(settings.Description) || settings.Description.Length > 100)
                throw new InvalidOperationException($"Music shortcut '{name}' must have a description between 1 and 100 characters.");

            if (string.IsNullOrWhiteSpace(settings.Query))
                throw new InvalidOperationException($"Music shortcut '{name}' must have a query.");

            if (!Enum.IsDefined(settings.Mode))
                throw new InvalidOperationException($"Music shortcut '{name}' has an invalid enqueue mode.");

            var shortcut = new MusicShortcut(name, settings.Description.Trim(), settings.Query.Trim(), settings.Mode);
            if (!_shortcuts.TryAdd(name, shortcut))
                throw new InvalidOperationException($"Music shortcut '{name}' is configured more than once.");
        }

        Commands = _shortcuts.Values
            .Select(shortcut => (ICommandDefinition)new ShortcutCommandDefinition(shortcut.Name, shortcut.Description))
            .ToArray();

        Registrations = _shortcuts.Keys
            .Select(name => (IInteractionRegistration)new InteractionRegistration(name, typeof(ShortcutInteractionHandler)))
            .ToArray();
    }

    public bool TryGet(string name, out MusicShortcut? shortcut)
        => _shortcuts.TryGetValue(name, out shortcut);

    private sealed class ShortcutCommandDefinition(string name, string description) : ICommandDefinition
    {
        public string Name => name;

        public string Description => description;

        public IReadOnlyList<CommandOptionDefinition> Options => [];
    }
}
