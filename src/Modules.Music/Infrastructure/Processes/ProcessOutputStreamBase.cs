using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Infrastructure.Processes;

internal abstract class ProcessOutputStreamBase : Stream
{
    private const int MaximumStderrCharacters = 8 * 1024;

    private readonly Process _process;
    private readonly int _processId;
    private readonly Stream _output;
    private readonly string _processName;
    private readonly Task<string> _stderrTask;
    private readonly Task? _inputPump;
    private readonly CancellationTokenSource? _inputPumpLifetime;
    private readonly ILogger _logger;
    private readonly object _sync = new();

    private Task? _completionTask;
    private Task? _disposeTask;
    private bool _disposed;

    protected ProcessOutputStreamBase(
        Process process,
        string processName,
        Task? inputPump,
        ILogger logger,
        CancellationTokenSource? inputPumpLifetime = null)
    {
        _process = process;
        _processId = process.Id;
        _output = process.StandardOutput.BaseStream;
        _processName = processName;
        _inputPump = inputPump;
        _inputPumpLifetime = inputPumpLifetime;
        _logger = logger;
        _stderrTask = DrainStderrAsync(process);
    }

    public override bool CanRead => !_disposed && _output.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        _output.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        var read = _output.Read(buffer, offset, count);
        if (read == 0)
            EnsureCompleted();

        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var read = await _output.ReadAsync(buffer, ct).ConfigureAwait(false);
        if (read == 0)
            await EnsureCompletedAsync().ConfigureAwait(false);

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        var buffer = new byte[bufferSize];
        int read;
        while ((read = await ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) != 0)
            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
    }

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        Task disposeTask;
        lock (_sync)
        {
            _disposed = true;
            _disposeTask ??= DisposeCoreAsync();
            disposeTask = _disposeTask;
        }

        await disposeTask.ConfigureAwait(false);
        base.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private void EnsureCompleted()
        => EnsureCompletedAsync().GetAwaiter().GetResult();

    private Task EnsureCompletedAsync()
    {
        lock (_sync)
            return _completionTask ??= CompleteProcessAsync();
    }

    private async Task CompleteProcessAsync()
    {
        var exitTask = _process.WaitForExitAsync();
        try
        {
            await exitTask.ConfigureAwait(false);
        }
        catch
        {
            // Inspect the process wait task below so its failure is retained.
        }

        // If the consumer process exits before its input pump finishes, stop the pump so it
        // cannot wait forever on a source the process is no longer reading.
        if (_inputPump is { IsCompleted: false })
        {
            try
            {
                _inputPumpLifetime?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        Task[] remainingTasks = _inputPump is null
            ? [_stderrTask]
            : [_stderrTask, _inputPump];
        try
        {
            await Task.WhenAll(remainingTasks).ConfigureAwait(false);
        }
        catch
        {
            // Inspect each task below so failures from both the input and process are retained.
        }

        var failures = new List<Exception>();
        AddTaskFailure(failures, _inputPump);
        AddTaskFailure(failures, exitTask);
        AddTaskFailure(failures, _stderrTask);

        int? exitCode = null;
        try
        {
            if (_process.HasExited)
                exitCode = _process.ExitCode;
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        if (exitCode is not null && exitCode != 0)
        {
            var stderr = _stderrTask.Status == TaskStatus.RanToCompletion
                ? _stderrTask.Result
                : string.Empty;
            LogStderr(stderr);
            failures.Add(new AudioProcessException(_processName, exitCode.Value, stderr));
        }
        else if (_stderrTask.Status == TaskStatus.RanToCompletion)
        {
            LogStderr(_stderrTask.Result);
        }

        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();

        if (failures.Count > 1)
            throw new AggregateException($"The {_processName} audio pipeline failed.", failures);
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            _inputPumpLifetime?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Cleanup should continue even if the process has already exited or cannot be killed.
        }

        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        await ObserveQuietlyAsync(_inputPump).ConfigureAwait(false);
        await ObserveQuietlyAsync(_stderrTask).ConfigureAwait(false);
        _inputPumpLifetime?.Dispose();

        Task? completionTask;
        lock (_sync)
            completionTask = _completionTask;
        await ObserveQuietlyAsync(completionTask).ConfigureAwait(false);

        try
        {
            await _output.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        _process.Dispose();
    }

    private static async Task<string> DrainStderrAsync(Process process)
    {
        var tail = new StringBuilder(MaximumStderrCharacters);
        var buffer = new char[1024];

        while (true)
        {
            var read = await process.StandardError.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (read == 0)
                break;

            tail.Append(buffer, 0, read);
            if (tail.Length > MaximumStderrCharacters)
                tail.Remove(0, tail.Length - MaximumStderrCharacters);
        }

        return tail.ToString().Trim();
    }

    private void LogStderr(string stderr)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("{ProcessName} process {ProcessId} wrote to stderr: {Stderr}", _processName, _processId, stderr);
    }

    private static void AddTaskFailure(List<Exception> failures, Task? task)
    {
        if (task?.Exception is { } exception)
            failures.AddRange(exception.Flatten().InnerExceptions);
    }

    private static async Task ObserveQuietlyAsync(Task? task)
    {
        if (task is null)
            return;

        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The natural-completion path reports task errors. During disposal, only drain them.
        }
    }
}

internal sealed class AudioProcessException(string processName, int exitCode, string stderr)
    : Exception($"{processName} exited with code {exitCode}.{FormatStderr(stderr)}")
{
    public string ProcessName { get; } = processName;

    public int ExitCode { get; } = exitCode;

    public string Stderr { get; } = stderr;

    private static string FormatStderr(string stderr)
        => string.IsNullOrWhiteSpace(stderr) ? string.Empty : $" stderr: {stderr}";
}
