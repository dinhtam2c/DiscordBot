using System.Diagnostics;

namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal class YtDlpStream(Process process) : Stream
{
    private readonly Stream _output = process.StandardOutput.BaseStream;

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

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return _output.CopyToAsync(destination, bufferSize, cancellationToken);
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
        try
        {
            await _output.DisposeAsync();
        }
        catch
        {
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
        }
        catch
        {
        }

        process.Dispose();

        await base.DisposeAsync();
    }
}
