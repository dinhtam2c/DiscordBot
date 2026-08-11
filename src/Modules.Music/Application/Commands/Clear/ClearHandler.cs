using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Clear;

[InteractionHandler("clear")]
internal sealed class ClearHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<ClearHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        if (player.Queue.Any())
        {
            logger.LogInformation("Clearing queue for guild {GuildId}", guildId);
            player.ClearQueue();
            await unitOfWork.CommitAsync(ct);
        }

        await context.Responder.RespondAsync(InteractionResponse.Text("Queue cleared."), ct);
    }
}
