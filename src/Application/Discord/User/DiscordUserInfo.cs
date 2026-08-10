using Domain.ValueObjects;

namespace Application.Discord.User;

public sealed record DiscordUserInfo(
    UserId Id,
    string Username,
    string DisplayName,
    string? AvatarUrl);
