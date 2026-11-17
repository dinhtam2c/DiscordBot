using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Domain.ValueObjects;
using Modules.Music.Features.Playback;
using Modules.Music.Tracks;
using Modules.Music.Tracks.Resolution;

namespace Modules.Music.Features.Search;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class SearchCommands(
    ITrackResolverFactory resolvers,
    TrackSelections results,
    EnqueueTracksUseCase enqueueTracks,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("search", "Find songs on YouTube or SoundCloud and choose one to play.")]
    public async Task SearchAsync(
        [Summary(description: "The song or artist to search for.")] string query,
        [Summary(description: "Where to search. Defaults to YouTube.")]
        [Choice("YouTube", "youtube"), Choice("SoundCloud", "soundcloud")] string source = "youtube")
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new UserFriendlyException("Enter a song or artist to search for.");
        var provider = source switch
        {
            "youtube" => TrackSource.Youtube,
            "soundcloud" => TrackSource.SoundCloud,
            _ => throw new UserFriendlyException("Choose YouTube or SoundCloud to search."),
        };
        var tracks = (await resolvers.Select(provider).SearchAsync(query.Trim(), Ct)).Take(5).ToArray();
        if (tracks.Length == 0)
        {
            await CompleteAsync("Couldn't find any songs. Try another song or artist.");
            return;
        }
        var id = results.Add(GuildId, UserId, tracks);
        var response = SearchResponse.Create(tracks, id, provider);
        await CompleteAsync(embed: response.Embed, components: response.Components);
    }

    [ComponentInteraction("music:search:*")]
    public async Task SelectAsync(string searchId, string[] values)
    {
        if (!Guid.TryParseExact(searchId, "N", out var id) || values.Length != 1
            || !int.TryParse(values[0], out var index))
            throw new UserFriendlyException("This selection isn't available. Use `/search` again.");
        var track = results.Take(id, GuildId, UserId, index);
        var result = await enqueueTracks.ExecuteAsync(new EnqueueTracksInput(
            GuildId, UserId, new ChannelId(Context.Channel.Id), Context.Interaction.Id, EnqueueTracksMode.Queue), [track], Ct);
        await CompleteAsync(embed: EnqueueTracksResponse.Create(result, EnqueueTracksMode.Queue));
    }
}
