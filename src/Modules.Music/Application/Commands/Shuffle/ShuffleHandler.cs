using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Shuffle;

[InteractionHandler("shuffle")]
internal sealed class ShuffleHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<ShuffleHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        if (player.Queue.Count > 1)
        {
            logger.LogInformation("Shuffling queue for guild {GuildId}", guildId);
            player.ShuffleQueue();
            await unitOfWork.CommitAsync(ct);
        }

        await context.Responder.RespondAsync(InteractionResponse.Text("Queue shuffled."), ct);
    }
}
