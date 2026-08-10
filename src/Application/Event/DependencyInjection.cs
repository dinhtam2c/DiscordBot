using Domain.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Event;

public static class DependencyInjection
{
    public static IServiceCollection AddEventModule(this IServiceCollection services)
    {
        services.AddSingleton<EventHandlerRegistry>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        return services;
    }

    public static IServiceCollection AddEventHandlers<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new EventHandlerRegistration(typeof(TEvent), typeof(THandler)));

        return services;
    }
}
