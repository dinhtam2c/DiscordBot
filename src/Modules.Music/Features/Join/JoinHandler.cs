using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;
using Application.Discord.User;

namespace Modules.Music.Features.Join;

[InteractionHandler("join")]
internal sealed class JoinHandler(PlayerCoordinator players, IDiscordUserService discordUserService) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var channel = await discordUserService.GetUserVoiceChannelAsync(guildId, context.UserId, ct)
            ?? throw new UserFriendlyException("You are not in a voice channel.");
        await players.ExecuteAsync(guildId, new JoinVoice(channel.Id), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text($"Joining **{channel.Name}**"), ct);
    }
}
