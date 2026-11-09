namespace Modules.Music.Processes.YtDlp;

internal sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; init; } = "yt-dlp";
}
