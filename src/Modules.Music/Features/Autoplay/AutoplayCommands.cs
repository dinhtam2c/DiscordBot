using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Autoplay;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class AutoplayCommands(PlayerCoordinator players, IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("autoplay", "Automatically play related songs from YouTube when the queue ends.")]
    public async Task SetAsync([Summary(description: "Turn autoplay on or off.")] bool enabled)
    {
        await players.ExecuteAsync(GuildId, new SetAutoplay(enabled), Ct);
        await CompleteAsync(enabled ? "Autoplay is on." : "Autoplay is off.");
    }
}
