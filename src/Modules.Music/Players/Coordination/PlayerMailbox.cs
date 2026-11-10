using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Players.Coordination;

internal sealed class PlayerMailbox(
    Func<Task> cleanup,
    CancellationToken stoppingToken,
    ILogger logger)
{
    private readonly Channel<IWorkItem> _channel = Channel.CreateUnbounded<IWorkItem>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    public Task Completion { get; private set; } = Task.CompletedTask;

    // Accessed only by work items running on this mailbox.
    public long EnqueueRevision { get; set; }
    public long SeekRevision { get; set; }

    public void Start() => Completion = Task.Run(RunAsync);

    public Task<T> SendAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var item = new Request<T>(action, ct);
        if (!_channel.Writer.TryWrite(item))
            throw new InvalidOperationException("The player is shutting down.");
        return item.Completion.Task.WaitAsync(ct);
    }

    public void Post(Func<CancellationToken, Task> action)
        => _channel.Writer.TryWrite(new Notification(action, logger));

    public void Complete() => _channel.Writer.TryComplete();

    private async Task RunAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    item.Cancel();
                    break;
                }
                await item.ExecuteAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _channel.Writer.TryComplete();
            while (_channel.Reader.TryRead(out var pending))
                pending.Cancel();

            try { await cleanup(); }
            catch (Exception ex) { logger.LogError(ex, "Failed to clean up player runtime."); }
        }
    }

    private interface IWorkItem
    {
        Task ExecuteAsync(CancellationToken ct);
        void Cancel();
    }

    private sealed class Request<T>(Func<CancellationToken, Task<T>> action, CancellationToken requestCt) : IWorkItem
    {
        public TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(CancellationToken ct)
        {
            if (requestCt.IsCancellationRequested)
            {
                Completion.TrySetCanceled(requestCt);
                return;
            }

            try
            {
                // Once execution begins, finish save + runtime sync even if the interaction disconnects.
                Completion.TrySetResult(await action(ct));
            }
            catch (OperationCanceledException ex) { Completion.TrySetCanceled(ex.CancellationToken); }
            catch (Exception ex) { Completion.TrySetException(ex); }
        }

        public void Cancel() => Completion.TrySetCanceled();
    }

    private sealed class Notification(Func<CancellationToken, Task> action, ILogger logger) : IWorkItem
    {
        public async Task ExecuteAsync(CancellationToken ct)
        {
            try { await action(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Failed to process a playback notification."); }
        }

        public void Cancel() { }
    }
}
