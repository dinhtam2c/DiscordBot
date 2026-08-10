using Application.Abstractions;
using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.User;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Commands.Join;

[InteractionHandler("join")]
internal sealed class JoinHandler(
    IPlayerRepository playerRepository,
    IDiscordUserService discordUserService,
    IUnitOfWork unitOfWork) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is not found.");
        var voiceChannelInfo = await discordUserService.GetUserVoiceChannelAsync(guildId, context.UserId, ct)
            ?? throw new UserFriendlyException("You are not in a voice channel.");

        var player = await playerRepository.GetByIdAsync(guildId, ct);
        if (player is null)
        {
            player = Player.Create(guildId);
            await playerRepository.AddAsync(player, ct);
        }

        player.Join(voiceChannelInfo.Id);
        await unitOfWork.CommitAsync(ct);

        await context.Responder.RespondAsync(InteractionResponse.Text($"Joined **{voiceChannelInfo.Name}**"), ct);
    }
}
