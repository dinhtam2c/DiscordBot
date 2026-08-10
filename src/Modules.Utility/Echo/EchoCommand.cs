using Application.Discord.Interactions.Command;

namespace Modules.Utility.Echo;

internal sealed class EchoCommand : ICommandDefinition
{
    public string Name => "echo";

    public string Description => "Echoes the input message.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new (
            Name: "message",
            Description: "The message to echo.",
            Type: CommandOptionType.String,
            Required: true)
    ];
}
