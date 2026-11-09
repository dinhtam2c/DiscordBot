using System.Text;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Processes.YtDlp;

internal sealed class YtDlpStream(Command command, ILogger logger, CancellationToken ct)
    : ProducerStream((output, token) => RunAsync(command, output, logger, token), ct)
{
    private static async Task RunAsync(Command command, Stream output, ILogger logger, CancellationToken ct)
    {
        var stderr = new StringBuilder();
        var result = await command
            .WithStandardOutputPipe(PipeTarget.ToStream(output))
            .WithStandardErrorPipe(PipeTarget.Create(async (input, token) =>
            {
                using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
                var buffer = new char[1024];
                int read;
                while ((read = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
                {
                    stderr.Append(buffer, 0, read);
                    if (stderr.Length > 8192)
                        stderr.Remove(0, stderr.Length - 8192);
                }
            }))
            .ExecuteAsync(ct);
        if (result.ExitCode != 0)
            throw YtDlpClient.CreateException(result.ExitCode, stderr.ToString().Trim());
        if (stderr.Length > 0)
            logger.LogDebug("yt-dlp stderr: {Stderr}", stderr.ToString().Trim());
    }
}
