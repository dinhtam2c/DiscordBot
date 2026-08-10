using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Leave;

[InteractionHandler("leave")]
internal sealed class LeaveHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<LeaveHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Not in a voice channel for playing music.");

        logger.LogInformation("Player for Guild {GuildId} is leaving the voice channel.", player.GuildId.Value);

        player.Leave();
        await playerRepository.RemoveAsync(guildId, ct);
        await unitOfWork.CommitAsync(ct);
    }
}