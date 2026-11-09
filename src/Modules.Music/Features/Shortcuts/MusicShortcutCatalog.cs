using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Modules.Music.Features.Playback;

namespace Modules.Music.Features.Shortcuts;

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

internal sealed record MusicShortcut(string Name, string Description, string Query, EnqueueTracksMode Mode);

internal sealed class MusicShortcutCatalog
{
    private static readonly Regex ValidName = new("^[a-z0-9_-]{1,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyCollection<MusicShortcut> Shortcuts { get; }

    public MusicShortcutCatalog(IOptions<MusicShortcutOptions> options)
    {
        var shortcuts = new Dictionary<string, MusicShortcut>(StringComparer.OrdinalIgnoreCase);
        foreach (var (configuredName, settings) in options.Value.Shortcuts)
        {
            var name = configuredName.Trim().ToLowerInvariant();
            if (!ValidName.IsMatch(name))
                throw new InvalidOperationException($"Music shortcut name '{configuredName}' is invalid.");
            if (string.IsNullOrWhiteSpace(settings.Description) || settings.Description.Length > 100)
                throw new InvalidOperationException($"Music shortcut '{name}' must have a description between 1 and 100 characters.");
            if (string.IsNullOrWhiteSpace(settings.Query))
                throw new InvalidOperationException($"Music shortcut '{name}' must have a query.");
            if (!Enum.IsDefined(settings.Mode))
                throw new InvalidOperationException($"Music shortcut '{name}' has an invalid enqueue mode.");

            if (!shortcuts.TryAdd(name, new MusicShortcut(name, settings.Description.Trim(), settings.Query.Trim(), settings.Mode)))
                throw new InvalidOperationException($"Music shortcut '{name}' is configured more than once.");
        }
        Shortcuts = shortcuts.Values.ToArray();
    }
}
