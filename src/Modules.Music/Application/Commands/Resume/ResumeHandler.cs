using Application.Abstractions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Application.Repositories;

namespace Modules.Music.Application.Commands.Resume;

[InteractionHandler("resume")]
internal sealed class ResumeHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var player = await playerRepository.GetByIdAsync(guildId, ct);

        if (player is null || !player.HasCurrentTrack)
        {
            await context.Responder.RespondAsync(InteractionResponse.Text("There is no track currently playing."), ct);
            return;
        }

        if (!player.IsPaused)
        {
            await context.Responder.RespondAsync(InteractionResponse.Text("Playback is not paused."), ct);
            return;
        }

        player.Resume();
        await unitOfWork.CommitAsync(ct);
        await context.Responder.RespondAsync(InteractionResponse.Text("Playback resumed."), ct);
    }
}
