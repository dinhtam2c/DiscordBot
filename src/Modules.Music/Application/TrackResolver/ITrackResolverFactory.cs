namespace Modules.Music.Application.TrackResolver;

internal interface ITrackResolverFactory
{
    ITrackResolver Select(string query);
}
