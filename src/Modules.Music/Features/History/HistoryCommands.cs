using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Microsoft.Extensions.Options;
using Modules.Music.Players;
using Discord.Interactions;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.History;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class HistoryCommands(PlayerCoordinator players, IOptions<PlaybackHistoryOptions> options, IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("history", "See recent songs, who requested them, and who skipped them.")]
    [Ephemeral]
    public Task HistoryAsync() => ShowAsync(0);

    [ComponentInteraction("music:history-page:*")]
    public Task PageAsync(string page)
        => int.TryParse(page, out var index) ? ShowAsync(index)
            : throw new UserFriendlyException("This history page isn't available. Use `/history` again.");

    private async Task ShowAsync(int page)
    {
        var entries = await players.GetHistoryAsync(GuildId, Ct);
        var response = HistoryResponse.Create(entries, page, options.Value.Capacity);
        await CompleteAsync(embed: response.Embed, components: response.Components);
    }
}
