using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Microsoft.Extensions.Options;
using Modules.Music.Players;
using Discord.Interactions;
using Domain.ValueObjects;
using Modules.Music.Features.Playback;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.History;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class HistoryCommands(PlayerCoordinator players, TrackSelections selections,
    EnqueueTracksUseCase enqueue, IOptions<PlaybackHistoryOptions> options, IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("history", "See recently played songs and choose one to play again.")]
    [Ephemeral]
    public Task HistoryAsync() => ShowAsync(0);

    [ComponentInteraction("music:history-page:*")]
    public Task PageAsync(string page)
        => int.TryParse(page, out var index) ? ShowAsync(index)
            : throw new UserFriendlyException("This history page isn't available. Use `/history` again.");

    private async Task ShowAsync(int page)
    {
        var entries = await players.GetHistoryAsync(GuildId, Ct);
        var response = HistoryResponse.Create(entries, page,
            tracks => selections.Add(GuildId, UserId, tracks), options.Value.Capacity);
        await CompleteAsync(embed: response.Embed, components: response.Components);
    }

    [ComponentInteraction("music:history-select:*")]
    public async Task SelectAsync(string selectionId, string[] values)
    {
        if (!Guid.TryParseExact(selectionId, "N", out var id) || values.Length != 1 || !int.TryParse(values[0], out var index))
            throw new UserFriendlyException("This selection isn't available. Use `/history` again.");
        var track = selections.Take(id, GuildId, UserId, index);
        var result = await enqueue.ExecuteAsync(new EnqueueTracksInput(GuildId, UserId,
            new ChannelId(Context.Channel.Id), Context.Interaction.Id, EnqueueTracksMode.Queue), [track], Ct);
        await CompleteAsync(embed: EnqueueTracksResponse.Create(result, EnqueueTracksMode.Queue));
    }
}
