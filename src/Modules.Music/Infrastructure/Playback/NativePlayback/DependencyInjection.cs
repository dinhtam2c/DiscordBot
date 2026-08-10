using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Playback;
using Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal static class DependencyInjection
{
    public static IServiceCollection AddNativePlayback(this IServiceCollection services)
    {
        services
            .AddOptions<FfmpegOptions>()
            .BindConfiguration(FfmpegOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IAudioTranscoder, FfmpegAudioTranscoder>();

        services.AddSingleton<IPlaybackFactory, NativePlaybackFactory>();
        return services;
    }
}
