using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Playback.Native;
using Modules.Music.Playback.Native.Ffmpeg;

namespace Modules.Music.Playback;

internal static class DependencyInjection
{
    public static IServiceCollection AddPlayback(this IServiceCollection services)
    {
        services.AddSingleton<IPlaybackEngine, PlaybackEngine>();
        services.AddOptions<FfmpegOptions>()
            .BindConfiguration(FfmpegOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IAudioTranscoder, FfmpegAudioTranscoder>();
        services.AddSingleton<IPlaybackFactory, NativePlaybackFactory>();
        return services;
    }
}
