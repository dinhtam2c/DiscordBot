using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.Runtime;

namespace Modules.Utility.About;

[InteractionHandler("about")]
internal sealed class AboutHandler(IDiscordRuntime discordRuntime) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        await context.Responder.RespondAsync(
            InteractionResponse.Text($"Bot ngu: {discordRuntime.BotName}\n"),
            ct);
    }
}
