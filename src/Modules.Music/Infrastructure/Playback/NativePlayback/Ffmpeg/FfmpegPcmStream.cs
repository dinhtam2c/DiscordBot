using System.Diagnostics;

namespace Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

internal sealed class FfmpegPcmStream(
    Process process,
    Task inputPump,
    Task stderrTask) : Stream
{
    private readonly Stream _output = process.StandardOutput.BaseStream;

    private bool _disposed = false;

    public override bool CanRead => _output.CanRead;

    public override bool CanSeek => _output.CanSeek;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
        _output.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _output.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _output.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
        }

        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
        }

        try
        {
            await inputPump;
        }
        catch
        {
        }

        try
        {
            await stderrTask;
        }
        catch
        {
        }

        try
        {
            await _output.DisposeAsync();
        }
        catch
        {
        }

        process.Dispose();
    }
}
