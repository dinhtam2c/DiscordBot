namespace Modules.Music.Processes;

internal sealed class AudioProcessException(string processName, int exitCode, string stderr)
    : Exception(string.IsNullOrWhiteSpace(stderr)
        ? $"Process '{processName}' exited with code {exitCode}."
        : $"Process '{processName}' exited with code {exitCode}. Standard error: {stderr.Trim()}")
{
    public string ProcessName { get; } = processName;
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;

    // Retry transport failures, not unavailable songs, authentication requirements or decoder errors.
    public bool IsTransient => System.Text.RegularExpressions.Regex.IsMatch(Stderr,
            @"\b(?:HTTP(?: Error)?|Server returned)\s+(?:403|408|429|5\d\d)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        || new[] { "connection reset", "connection refused", "connection timed out", "timed out",
            "temporary failure in name resolution", "network is unreachable", "input/output error",
            "getaddrinfo failed", "failed to resolve hostname", "could not resolve host",
            "remote end closed connection", "connection closed prematurely", "tls error" }
            .Any(message => Stderr.Contains(message, StringComparison.OrdinalIgnoreCase));
}
