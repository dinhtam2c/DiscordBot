namespace Application.Discord.Interactions.Dispatcher;

public interface IInteractionRegistration
{
    public string Name { get; }

    public Type HandlerType { get; }
}
