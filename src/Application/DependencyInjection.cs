using Application.Discord.Interactions.Command;
using Application.Discord.Interactions.Dispatcher;
using Application.Discord.Interactions.Handler;
using Application.Discord.Voice;
using Application.Event;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddEventModule();

        services.AddSingleton<InteractionHandlerResolver>();
        services.AddSingleton<InteractionDispatcher>();

        return services;
    }

    public static IServiceCollection AddInteraction<TCommand, THandler>(this IServiceCollection services)
        where TCommand : class, ICommandDefinition
        where THandler : class, IInteractionHandler
    {
        var attribute = typeof(THandler)
            .GetCustomAttribute<InteractionHandlerAttribute>()
            ?? throw new InvalidOperationException(
                $"Handler '{typeof(THandler).FullName}' must have " +
                $"{nameof(InteractionHandlerAttribute)}.");

        services.AddSingleton<ICommandDefinition, TCommand>();
        services.AddScoped<THandler>();

        services.AddSingleton<IInteractionRegistration>(new InteractionRegistration(attribute.CommandName, typeof(THandler)));

        return services;
    }
}
