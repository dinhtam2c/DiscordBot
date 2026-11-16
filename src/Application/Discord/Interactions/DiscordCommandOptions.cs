using Discord.Interactions;

namespace Application.Discord.Interactions;

public sealed class DiscordCommandOptions
{
    public List<Type> Modules { get; } = [];

    public List<Func<InteractionService, IServiceProvider, Task>> ConfigureModules { get; } = [];
}
