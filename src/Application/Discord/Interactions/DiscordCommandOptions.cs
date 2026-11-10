using Discord.Interactions;

namespace Application.Discord.Interactions;

public sealed class DiscordCommandOptions
{
    public List<Type> Modules { get; } = [];

    public HashSet<string> EphemeralCommands { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<Func<InteractionService, IServiceProvider, Task>> ConfigureModules { get; } = [];
}
