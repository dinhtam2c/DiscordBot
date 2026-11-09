using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Music.Processes.YtDlp;

internal sealed class YtDlpJsonRunner(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpJsonRunner> logger)
{
    public async Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.Value.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(ct);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                logger.LogError("yt-dlp exited with code {ExitCode}: {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}.");
            }

            return stdout;
        }
        catch
        {
            TryKill(process);
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            await ObserveQuietlyAsync(stdoutTask).ConfigureAwait(false);
            await ObserveQuietlyAsync(stderrTask).ConfigureAwait(false);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Continue cleanup if the process already exited or cannot be killed.
        }
    }

    private static async Task ObserveQuietlyAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
