namespace Modules.Music.Tracks.Resolution;

internal interface ITrackResolverFactory
{
    ITrackResolver Select(string query);
}
