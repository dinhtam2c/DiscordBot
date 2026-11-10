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
    private readonly CancellationTokenSource _writeLifetime = new();
    private AudioOutStream? _pcmStream;
    private int _disconnected;
    private bool _disposed;

    public GuildId GuildId => guildId;

    public ChannelId VoiceChannelId => voiceChannelId;

    // A resumed audio client must not revive streams from the previous connection.
    public bool IsConnected => Volatile.Read(ref _disconnected) == 0
        && audioClient.ConnectionState == ConnectionState.Connected;

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
                bufferMillis: 200,
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

    public async Task ClearAsync(CancellationToken ct)
    {
        await _streamLock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Recreate the output stream; Discord.Net's ClearAsync does not restore buffer slots.
            DisposeStreams();
        }
        finally { _streamLock.Release(); }
    }

    public async ValueTask DisposeAsync()
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
        Interlocked.Exchange(ref _disconnected, 1);
        CancelPendingWrites();
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
            _writeLifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch
        {
            // Cancellation is best-effort; session replacement and cleanup must continue.
        }
    }

}
