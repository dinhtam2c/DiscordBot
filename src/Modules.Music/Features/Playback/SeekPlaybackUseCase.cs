using System.Globalization;
using Application.Abstractions;
using Domain.ValueObjects;
using Modules.Music.Players.Coordination;
using Modules.Music.Tracks.AudioSources;

namespace Modules.Music.Features.Playback;

internal sealed class SeekPlaybackUseCase(PlayerCoordinator players, IAudioSourceResolver sources)
{
    public async Task ExecuteAsync(GuildId guildId, TimeSpan position, CancellationToken ct)
    {
        var preparation = await players.BeginSeekAsync(guildId, position, ct);
        var item = preparation.Item;

        // Resolving a URL must not block pause/skip/stop behind a slow network request.
        var source = await sources.Resolve(item.Track.Source).ResolveAsync(item.Track, ct);
        if (source.IsLive)
            throw new UserFriendlyException("This song doesn't support seeking.");
        await players.ExecuteAsync(guildId, new SeekPlayback(position, source, preparation), ct, expectedQueueItemId: item.Id);
    }

    internal static TimeSpan ParsePosition(string value)
    {
        var parts = value.Trim().Split(':');
        if (parts.Length is >= 1 and <= 3)
        {
            long seconds = 0;
            try
            {
                for (var index = 0; index < parts.Length; index++)
                {
                    if (!long.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var part)
                        || (index > 0 && part >= 60))
                        break;
                    seconds = checked(seconds * 60 + part);
                    if (index == parts.Length - 1)
                        return new TimeSpan(checked(seconds * TimeSpan.TicksPerSecond));
                }
            }
            catch (OverflowException) { }
        }
        throw new UserFriendlyException("Enter a position in seconds, mm:ss, or hh:mm:ss, for example `90` or `1:30`.");
    }
}
