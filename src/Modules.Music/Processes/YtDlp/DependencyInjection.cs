using Microsoft.Extensions.DependencyInjection;

namespace Modules.Music.Processes.YtDlp;

internal static class DependencyInjection
{
    public static IServiceCollection AddYtDlp(this IServiceCollection services)
    {
        services
            .AddOptions<YtDlpOptions>()
            .BindConfiguration(YtDlpOptions.SectionName)
            .Validate(options => options.MetadataTimeout > TimeSpan.Zero
                && options.MetadataTimeout <= TimeSpan.FromMinutes(10),
                "YtDlp:MetadataTimeout must be greater than zero and no longer than 10 minutes.")
            .ValidateOnStart();

        services.AddSingleton<YtDlpClient>();

        return services;
    }
}
