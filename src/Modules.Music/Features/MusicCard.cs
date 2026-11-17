using Application.Discord.Messages;
using Discord;
using Modules.Music.Tracks;

namespace Modules.Music.Features;

internal static class MusicCard
{
    public static ContainerBuilder Create(string title, Track track, string? details = null)
    {
        var text = $"### {title}\n**{MessageFormatting.Link(track.Title, track.SourceUrl)}**\nArtist: {TrackFormatting.Artists(track)}";
        if (!string.IsNullOrEmpty(details)) text += "\n" + details;
        var container = new ContainerBuilder();
        if (Uri.TryCreate(track.ThumbnailUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            container.WithSection(new SectionBuilder(
                new ThumbnailBuilder(new UnfurledMediaItemProperties(uri.AbsoluteUri), description: track.Title),
                [new TextDisplayBuilder(text)]));
        else
            container.WithTextDisplay(text);
        return container;
    }

    public static MessageComponent Build(ContainerBuilder container)
        => new ComponentBuilderV2().WithContainer(container).Build();
}
