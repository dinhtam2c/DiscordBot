using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;
using PlayerVolume = Modules.Music.Players.Volume;

namespace Modules.Music.Features.Volume;

[InteractionHandler("volume")]
internal sealed class VolumeHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var level = context.Options.GetRequired<long>("level");
        if (level is < 0 or > 100)
            throw new UserFriendlyException("Volume must be between 0 and 100.");
        var volume = PlayerVolume.Create((int)level);
        await players.ExecuteAsync(guildId, new SetVolume(volume), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text($"Volume set to **{volume.Value}%**."), ct);
    }
}
