using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

internal static class DependencyInjection
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<MemoryPersistenceContext>();
        services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        return services;
    }
}
