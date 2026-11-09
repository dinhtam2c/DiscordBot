using CliWrap;
using CliWrap.Buffered;
using Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Music.Processes.YtDlp;

internal sealed class YtDlpClient(IOptions<YtDlpOptions> options, ILogger<YtDlpClient> logger)
{
    public async Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        var result = await CreateCommand(arguments).ExecuteBufferedAsync(ct);
        if (result.ExitCode != 0)
            throw CreateException(result.ExitCode, result.StandardError);
        return result.StandardOutput;
    }

    public Stream OpenStream(IEnumerable<string> arguments, CancellationToken ct)
        => new YtDlpStream(CreateCommand(arguments), logger, ct);

    internal static Exception CreateException(int exitCode, string stderr)
    {
        var error = new AudioProcessException("yt-dlp", exitCode, stderr);
        return stderr.Contains("Sign in to confirm your age", StringComparison.OrdinalIgnoreCase)
            ? new UserFriendlyException("This song can't be played because YouTube requires age verification. Try another song.", error)
            : error;
    }

    private Command CreateCommand(IEnumerable<string> arguments)
        => Cli.Wrap(options.Value.ExecutablePath)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None);
}
