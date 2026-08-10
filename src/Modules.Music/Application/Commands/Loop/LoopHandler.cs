using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Commands.Loop;

[InteractionHandler("loop")]
internal class LoopHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");
        var player = await playerRepository.GetByIdAsync(guildId, ct)
            ?? throw new UserFriendlyException("No player found for this server.");

        var mode = context.Options.GetRequired<LoopMode>("mode");
        player.SetLoopMode(mode);
        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text($"Loop mode set to **{mode}**."), ct);
    }
}
