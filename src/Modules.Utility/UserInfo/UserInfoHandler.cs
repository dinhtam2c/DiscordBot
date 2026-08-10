using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Application.Discord.User;
using Domain.ValueObjects;

namespace Modules.Utility.UserInfo;

[InteractionHandler("userinfo")]
internal sealed class UserInfoHandler(IDiscordUserService userService) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var userId = context.Options.GetOptional<UserId?>("user") ?? context.UserId;
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is not available.");

        var user = await userService.GetUserAsync(guildId, userId, ct);

        var response = InteractionResponse.Create(
            embeds:
            [
                new ResponseEmbed
                {
                    Title = "User Information",
                    ThumbnailUrl = user?.AvatarUrl,
                    Fields =
                    [
                        new("Username", user?.Username ?? "-", true),
                        new("Display Name", user?.DisplayName ?? "-", true)
                    ]
                }
            ]);

        await context.Responder.RespondAsync(response, ct);
    }
}
