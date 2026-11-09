using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    [Required(ErrorMessage = "The Discord bot token is required.")]
    public required string Token { get; init; }

    public ulong GuildId { get; init; }
}
