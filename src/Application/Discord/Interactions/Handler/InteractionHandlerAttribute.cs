namespace Application.Discord.Interactions.Handler;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InteractionHandlerAttribute(string commandName) : Attribute
{
    public string CommandName { get; } = commandName;
}
