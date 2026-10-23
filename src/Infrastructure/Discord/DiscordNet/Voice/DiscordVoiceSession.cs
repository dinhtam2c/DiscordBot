using Discord;
using Discord.Audio;
using Domain.ValueObjects;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceSession(
    GuildId guildId,
    ChannelId voiceChannelId,
    IAudioClient audioClient) : IAsyncDisposable
{
    private readonly SemaphoreSlim _streamLock = new(1, 1);
    private AudioOutStream? _pcmStream;
    private AudioOutStream? _opusStream;
    private bool _disposed;

    public GuildId GuildId => guildId;

    public ChannelId VoiceChannelId => voiceChannelId;

    public ConnectionState ConnectionState => audioClient.ConnectionState;

    public bool IsConnected => audioClient.ConnectionState == ConnectionState.Connected;

    public event Action? ConnectionChanged;

    public async Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _streamLock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pcmStream ??= audioClient.CreatePCMStream(
                AudioApplication.Music,
                bitrate: 128000,
                bufferMillis: 1000,
                packetLoss: 5);

            await _pcmStream.WriteAsync(data, ct);
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async Task WriteOpusAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _streamLock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _opusStream ??= audioClient.CreateOpusStream(bufferMillis: 3000);
            await _opusStream.WriteAsync(data, ct);
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await _streamLock.WaitAsync(ct);
        try
        {
            if (_pcmStream is not null)
                await _pcmStream.FlushAsync(ct);

            if (_opusStream is not null)
                await _opusStream.FlushAsync(ct);
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async Task ResetStreamsAsync()
    {
        await _streamLock.WaitAsync();
        try
        {
            DisposeStreams();
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _streamLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeStreams();
            audioClient.Connected -= OnConnectedAsync;
            audioClient.Disconnected -= OnDisconnectedAsync;
            audioClient.Dispose();
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public void SubscribeToConnectionEvents()
    {
        audioClient.Connected += OnConnectedAsync;
        audioClient.Disconnected += OnDisconnectedAsync;
    }

    private Task OnConnectedAsync()
    {
        ConnectionChanged?.Invoke();
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(Exception _)
    {
        ConnectionChanged?.Invoke();
        return Task.CompletedTask;
    }

    private void DisposeStreams()
    {
        _pcmStream?.Dispose();
        _opusStream?.Dispose();
        _pcmStream = null;
        _opusStream = null;
    }
}
