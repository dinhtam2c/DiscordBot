using Domain.ValueObjects;

namespace Application.Discord.Channel;

public sealed record DiscordTextChannelInfo(
    ChannelId Id,
    string Name,
    int Position,
    bool IsNsfw,
    string? Topic,
    DateTimeOffset? CreatedAt
) : DiscordChannelInfo(Id, Name, Position, IsNsfw, Topic, CreatedAt);
