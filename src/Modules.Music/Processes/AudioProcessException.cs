namespace Modules.Music.Processes;

internal sealed class AudioProcessException(string processName, int exitCode, string stderr)
    : Exception(string.IsNullOrWhiteSpace(stderr)
        ? $"Process '{processName}' exited with code {exitCode}."
        : $"Process '{processName}' exited with code {exitCode}. Standard error: {stderr.Trim()}")
{
    public string ProcessName { get; } = processName;
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}
