using Domain.ValueObjects;

namespace Application.Discord.Channel;

public abstract record DiscordChannelInfo(
    ChannelId Id,
    string Name,
    int Position,
    bool IsNsfw,
    string? Topic,
    DateTimeOffset? CreatedAt
);
