using Application.Discord.Voice;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceOutput(Action<DiscordVoiceSession> onWriteFailure) : IVoiceOutput
{
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly object _sync = new();
    private TaskCompletionSource _changed = NewSignal();
    private DiscordVoiceSession? _session;

    public async Task SetSessionAsync(DiscordVoiceSession? session)
    {
        await _ioLock.WaitAsync();
        try
        {
            lock (_sync)
            {
                if (ReferenceEquals(_session, session))
                    return;

                _session = session;
                SignalLocked();
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => WriteAsync((session, token) => session.WritePcmAsync(data, token), ct);

    public Task WriteOpusAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => WriteAsync((session, token) => session.WriteOpusAsync(data, token), ct);

    public Task FlushAsync(CancellationToken ct)
        => WriteAsync((session, token) => session.FlushAsync(token), ct);

    private async Task WriteAsync(
        Func<DiscordVoiceSession, CancellationToken, Task> write,
        CancellationToken ct)
    {
        while (true)
        {
            await _ioLock.WaitAsync(ct);
            Task waitForChange;
            try
            {
                DiscordVoiceSession? session;
                lock (_sync)
                {
                    session = _session;
                    waitForChange = _changed.Task;
                }

                if (session is not null && session.IsConnected)
                {
                    try
                    {
                        await write(session, ct);
                        return;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        lock (_sync)
                        {
                            if (ReferenceEquals(_session, session))
                            {
                                _session = null;
                                SignalLocked();
                                waitForChange = _changed.Task;
                            }
                        }

                        onWriteFailure(session);
                    }
                }
            }
            finally
            {
                _ioLock.Release();
            }

            await waitForChange.WaitAsync(ct);
        }
    }

    private void SignalLocked()
    {
        var previous = _changed;
        _changed = NewSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
