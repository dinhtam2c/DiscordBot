namespace Modules.Music.Players;

internal sealed class PlaybackHistoryOptions
{
    public const string SectionName = "Music:History";

    public int Capacity { get; init; } = 500;
    public int AutoplayWindow { get; init; } = 200;
}
