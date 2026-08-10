namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal class YoutubeOptions
{
    public const string SectionName = "Youtube";

    public string YtDlpPath { get; init; } = "yt-dlp";

    public int SearchResultCount { get; init; } = 5;
}
