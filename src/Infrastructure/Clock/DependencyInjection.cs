using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Clock;

internal static class DependencyInjection
{
    public static IServiceCollection AddClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
