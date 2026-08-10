using Application.Discord.Voice;
using Discord.Audio;
using Domain.ValueObjects;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceSession(
    GuildId guildId,
    ChannelId voiceChannelId,
    IAudioClient audioClient) : IVoiceSession, IDisposable
{
    public GuildId GuildId => guildId;

    public ChannelId VoiceChannelId => voiceChannelId;

    private AudioOutStream? _stream;

    public async Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var stream = GetOrCreateStream();

        await stream.WriteAsync(data, ct);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        if (_stream is not null)
            await _stream.FlushAsync(ct);
    }

    private AudioOutStream GetOrCreateStream()
    {
        return _stream ??= audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: 2000);
    }

    public void Dispose()
    {
        _stream?.Dispose();
        audioClient.Dispose();
    }
}
