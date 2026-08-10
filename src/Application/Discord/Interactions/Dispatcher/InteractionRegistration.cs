namespace Application.Discord.Interactions.Dispatcher;

public sealed record InteractionRegistration(string Name, Type HandlerType) : IInteractionRegistration;
