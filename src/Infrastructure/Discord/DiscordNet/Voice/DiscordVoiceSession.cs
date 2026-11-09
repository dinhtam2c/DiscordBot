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
    private CancellationTokenSource _writeLifetime = new();
    private AudioOutStream? _pcmStream;
    private bool _disposed;

    public GuildId GuildId => guildId;

    public ChannelId VoiceChannelId => voiceChannelId;

    public bool IsConnected => audioClient.ConnectionState == ConnectionState.Connected;

    public event Action? ConnectionChanged;

    public async Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _streamLock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var writeCts = CreateWriteToken(ct);
            _pcmStream ??= audioClient.CreatePCMStream(
                AudioApplication.Music,
                bitrate: 128000,
                bufferMillis: 1000,
                packetLoss: 5);

            await _pcmStream.WriteAsync(data, writeCts.Token);
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
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var writeCts = CreateWriteToken(ct);
            if (_pcmStream is not null)
                await _pcmStream.FlushAsync(writeCts.Token);
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async Task ResetStreamsAsync()
    {
        CancelPendingWrites();
        await _streamLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            DisposeStreams();
            ReplaceWriteLifetime();
        }
        finally
        {
            _streamLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
        => await DisposeAsync(disposeAudioClient: true);

    public async ValueTask DisposeAsync(bool disposeAudioClient)
    {
        CancelPendingWrites();
        await _streamLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeStreams();
            _writeLifetime.Dispose();
            audioClient.Connected -= OnConnectedAsync;
            audioClient.Disconnected -= OnDisconnectedAsync;
            if (disposeAudioClient)
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
        _pcmStream = null;
    }

    private CancellationTokenSource CreateWriteToken(CancellationToken ct)
        => CancellationTokenSource.CreateLinkedTokenSource(ct, _writeLifetime.Token);

    internal void CancelPendingWrites()
    {
        try
        {
            Volatile.Read(ref _writeLifetime).Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch
        {
            // Cancellation is best-effort; session replacement and cleanup must continue.
        }
    }

    private void ReplaceWriteLifetime()
    {
        var previous = _writeLifetime;
        Volatile.Write(ref _writeLifetime, new CancellationTokenSource());
        previous.Dispose();
    }
}
