using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Stop;

[InteractionHandler("stop")]
internal sealed class StopHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<StopHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        logger.LogInformation("Stopping playback for guild {GuildId}.", guildId);

        player.Stop();
        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text("Playback stopped."), ct);
    }
}
