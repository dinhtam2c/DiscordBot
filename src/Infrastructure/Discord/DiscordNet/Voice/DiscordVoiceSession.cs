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

    private AudioOutStream? _pcmStream;

    private AudioOutStream? _opusStream;

    public async Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var stream = GetOrCreatePcmStream();

        await stream.WriteAsync(data, ct);
    }

    public async Task WriteOpusAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var stream = GetOrCreateOpusStream();

        await stream.WriteAsync(data, ct);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        if (_pcmStream is not null)
            await _pcmStream.FlushAsync(ct);

        if (_opusStream is not null)
            await _opusStream.FlushAsync(ct);
    }

    private AudioOutStream GetOrCreatePcmStream()
    {
        return _pcmStream ??= audioClient.CreatePCMStream(
            AudioApplication.Music,
            bitrate: 128000,
            bufferMillis: 1000,
            packetLoss: 5);
    }

    private AudioOutStream GetOrCreateOpusStream()
    {
        return _opusStream ??= audioClient.CreateOpusStream(bufferMillis: 3000);
    }

    public void Dispose()
    {
        _pcmStream?.Dispose();
        _opusStream?.Dispose();
        audioClient.Dispose();
    }
}
