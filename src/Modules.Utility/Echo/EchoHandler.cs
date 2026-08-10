using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;

namespace Modules.Utility.Echo;

[InteractionHandler("echo")]
internal sealed class EchoHandler : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        await context.Responder.RespondAsync(
            InteractionResponse.Text(context.Options.GetRequired<string>("message")),
            ct);
    }
}
