using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Skip;

[InteractionHandler("skip")]
internal class SkipHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork,
    ILogger<SkipHandler> logger) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");

        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");

        if (!player.HasCurrentTrack)
        {
            await context.Responder.RespondAsync(InteractionResponse.Text("There is no track currently playing."), ct);
            return;
        }

        var track = player.CurrentTrack!;

        logger.LogInformation("Skip command received. GuildId: {GuildId}, CurrentTrack: {CurrentTrack}", guildId, track.Title);

        player.Skip();

        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text($"⏭️ Skipped: {track.Title}"), ct);
    }
}
