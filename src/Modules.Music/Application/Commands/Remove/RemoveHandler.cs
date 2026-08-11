using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Remove;

[InteractionHandler("remove")]
internal sealed class RemoveHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<RemoveHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is null");
        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        var index = context.Options.GetRequired<long>("index");
        if (index < 1 || index > player.Queue.Count)
            throw new UserFriendlyException($"Invalid index. Please provide a number between 1 and {player.Queue.Count}.");

        logger.LogInformation("Removing track at position {Index} from the queue for guild {GuildId}", index, guildId);

        player.RemoveFromQueue((int)index - 1);
        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text($"Removed track at position {index} from the queue."), ct);
    }
}
