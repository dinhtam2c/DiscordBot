using Microsoft.Extensions.DependencyInjection;

namespace Modules.Music.Processes.YtDlp;

internal static class DependencyInjection
{
    public static IServiceCollection AddYtDlp(this IServiceCollection services)
    {
        services
            .AddOptions<YtDlpOptions>()
            .BindConfiguration(YtDlpOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<YtDlpClient>();

        return services;
    }
}
