using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.Runtime;

namespace Modules.Utility.Ping;

[InteractionHandler("ping")]
internal sealed class PingHandler(IDiscordRuntime runtime) : IInteractionHandler
{
    public Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        return context.Responder.RespondAsync(
            InteractionResponse.Text(
                "Pong!\n" +
                $"Latency: {runtime.Latency} ms"),
            ct);
    }
}
