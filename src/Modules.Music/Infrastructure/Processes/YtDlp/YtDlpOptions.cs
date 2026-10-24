namespace Modules.Music.Infrastructure.Processes.YtDlp;

internal sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; init; } = "yt-dlp";
}
