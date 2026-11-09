using System.IO.Pipelines;

namespace Modules.Music.Processes;

// Bridges a producer's write API to a read stream without buffering the whole track.
// Process ownership stays with the producer (CliWrap or FFMpegCore).
internal abstract class ProducerStream : Stream
{
    private readonly CancellationTokenSource _lifetime;
    private readonly Stream _reader;
    private readonly Task _producer;
    private readonly object _sync = new();
    private Task? _disposeTask;
    private bool _disposed;

    protected ProducerStream(Func<Stream, CancellationToken, Task> produce, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024,
            useSynchronizationContext: false));
        _reader = pipe.Reader.AsStream();
        _producer = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                await produce(pipe.Writer.AsStream(leaveOpen: true), _lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex) { error = ex; }
            finally { await pipe.Writer.CompleteAsync(error).ConfigureAwait(false); }
        });
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.ReadAsync(buffer, ct);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.CopyToAsync(destination, bufferSize, ct);
    }

    public override void Flush() { }
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        Task disposal;
        lock (_sync)
        {
            _disposed = true;
            disposal = _disposeTask ??= DisposeCoreAsync();
        }
        await disposal.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            await _reader.DisposeAsync().ConfigureAwait(false);
            await _producer.ConfigureAwait(false);
        }
        finally { _lifetime.Dispose(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }
}
