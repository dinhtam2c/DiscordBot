using Application.Discord.Interactions.Command;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;

namespace Modules.Utility.Help;

[InteractionHandler("help")]
internal sealed class HelpHandler(IEnumerable<ICommandDefinition> definitions) : IInteractionHandler
{
    public Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var helpText = "Available commands:\n";
        foreach (var definition in definitions)
        {
            helpText += $"- `{definition.Name}`: {definition.Description}\n";
        }

        return context.Responder.RespondAsync(InteractionResponse.Text(helpText), ct);
    }
}