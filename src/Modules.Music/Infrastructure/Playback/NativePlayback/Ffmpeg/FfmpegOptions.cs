namespace Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

internal class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    public string FfmpegPath { get; init; } = "ffmpeg";
}
