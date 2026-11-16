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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Value.MetadataTimeout);
        try
        {
            var result = await CreateCommand(arguments).ExecuteBufferedAsync(timeout.Token);
            if (result.ExitCode != 0)
            {
                logger.LogDebug("yt-dlp metadata request failed with exit code {ExitCode}. Standard error: {Stderr}",
                    result.ExitCode, result.StandardError);
                throw CreateException(result.ExitCode, result.StandardError);
            }
            return result.StandardOutput;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new UserFriendlyException("This song or playlist took too long to load. Please try again.",
                new TimeoutException("The yt-dlp metadata request timed out.", ex));
        }
        catch (AudioProcessException ex)
        {
            throw ex.ForUser();
        }
    }

    public Stream OpenStream(IEnumerable<string> arguments, CancellationToken ct)
        => new YtDlpStream(CreateCommand(arguments), logger, ct);

    internal static Exception CreateException(int exitCode, string stderr)
    {
        var error = new AudioProcessException("yt-dlp", exitCode, stderr);
        // Keep transport failures inspectable by the playback retry policy.
        return error.IsTransient ? error : error.ForUser();
    }

    private Command CreateCommand(IEnumerable<string> arguments)
        => Cli.Wrap(options.Value.ExecutablePath)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None);
}
