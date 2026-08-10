using Domain.ValueObjects;

namespace Application.Discord.Voice;

public interface IVoiceSession
{
    GuildId GuildId { get; }

    ChannelId VoiceChannelId { get; }

    Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    Task WriteOpusAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    Task FlushAsync(CancellationToken ct);
}
