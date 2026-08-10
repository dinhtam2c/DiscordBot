using Domain.ValueObjects;

namespace Application.Discord.Channel;

public sealed record DiscordVoiceChannelInfo(
    ChannelId Id,
    string Name,
    int Bitrate,
    int? UserLimit,
    int Position,
    bool IsNsfw,
    string? Topic,
    DateTimeOffset? CreatedAt
) : DiscordChannelInfo(Id, Name, Position, IsNsfw, Topic, CreatedAt);
