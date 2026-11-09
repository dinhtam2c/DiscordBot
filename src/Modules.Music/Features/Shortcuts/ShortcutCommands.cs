using Application.Abstractions;
using Discord;
using Discord.Interactions;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Features.Playback;

namespace Modules.Music.Features.Shortcuts;

internal static class ShortcutCommands
{
    public static async Task RegisterAsync(InteractionService commands, IServiceProvider services)
    {
        var shortcuts = services.GetRequiredService<MusicShortcutCatalog>().Shortcuts;
        var reservedNames = commands.SlashCommands.Select(command => command.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var shortcut in shortcuts)
        {
            if (reservedNames.Contains(shortcut.Name))
                throw new InvalidOperationException($"Music shortcut '{shortcut.Name}' conflicts with an existing command.");
        }
        if (shortcuts.Count == 0)
            return;

        await commands.CreateModuleAsync("MusicShortcuts", services, module =>
        {
            module.WithContextTypes(InteractionContextType.Guild);
            foreach (var shortcut in shortcuts)
            {
                module.AddSlashCommand(shortcut.Name, async (context, _, requestServices, _) =>
                {
                    var guild = context.Guild ?? throw new UserFriendlyException("Use this command in a server, not in a DM.");
                    var ct = requestServices.GetRequiredService<IAppLifetime>().Stopping;
                    var useCase = requestServices.GetRequiredService<EnqueueTracksUseCase>();
                    var result = await useCase.ExecuteAsync(
                        new EnqueueTracksInput(new GuildId(guild.Id), new UserId(context.User.Id), shortcut.Query, shortcut.Mode), ct);
                    await context.Interaction.ModifyOriginalResponseAsync(
                        message => message.Embeds = new[] { EnqueueTracksResponse.Create(result, shortcut.Mode) },
                        new RequestOptions { CancelToken = ct });
                }, command => command.WithDescription(shortcut.Description));
            }
        });
    }
}
