using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Move;

[InteractionHandler("move")]
internal sealed class MoveHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<MoveHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is null");
        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        var index = context.Options.GetRequired<long>("index");
        var newIndex = context.Options.GetRequired<long>("new_index");

        if (index < 1 || index > player.Queue.Count)
            throw new UserFriendlyException($"Invalid index. Please provide a number between 1 and {player.Queue.Count}.");

        if (newIndex < 1 || newIndex > player.Queue.Count)
            throw new UserFriendlyException($"Invalid new index. Please provide a number between 1 and {player.Queue.Count}.");

        logger.LogInformation(
            "Moving track at position {Index} to position {NewIndex} in the queue for guild {GuildId}", index, newIndex, guildId);

        player.MoveInQueue((int)index - 1, (int)newIndex - 1);
        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text($"Moved track from position {index} to {newIndex} in the queue."), ct);
    }
}
