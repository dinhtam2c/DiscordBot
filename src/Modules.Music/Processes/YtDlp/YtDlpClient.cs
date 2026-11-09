using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Music.Processes.YtDlp;

internal sealed class YtDlpClient(IOptions<YtDlpOptions> options, ILogger<YtDlpClient> logger)
{
    public async Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        var result = await CreateCommand(arguments).ExecuteBufferedAsync(ct);
        if (result.ExitCode != 0)
            throw new AudioProcessException("yt-dlp", result.ExitCode, result.StandardError);
        return result.StandardOutput;
    }

    public Stream OpenStream(IEnumerable<string> arguments, CancellationToken ct)
        => new YtDlpStream(CreateCommand(arguments), logger, ct);

    private Command CreateCommand(IEnumerable<string> arguments)
        => Cli.Wrap(options.Value.ExecutablePath)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None);
}
