using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players;
using PlayerVolume = Modules.Music.Domain.Players.Volume;

namespace Modules.Music.Application.Commands.Volume;

[InteractionHandler("volume")]
internal sealed class VolumeHandler(
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var requestedVolume = context.Options.GetRequired<long>("level");
        if (requestedVolume is < 0 or > 100)
            throw new UserFriendlyException("Volume must be between 0 and 100.");

        var volume = PlayerVolume.Create((int)requestedVolume);
        var player = await playerRepository.GetByIdAsync(guildId, ct);
        var isNewPlayer = player is null;
        if (player is null)
        {
            player = Player.Create(guildId);
            await playerRepository.AddAsync(player, ct);
        }

        var changed = player.SetVolume(volume);
        if (isNewPlayer || changed)
            await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(
            InteractionResponse.Text($"Volume set to **{volume.Value}%**."),
            ct);
    }
}
