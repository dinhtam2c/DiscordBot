namespace Modules.Music.Processes;

internal sealed class AudioProcessException(string processName, int exitCode, string stderr)
    : Exception($"{processName} exited with code {exitCode}. {stderr}".TrimEnd())
{
    public string ProcessName { get; } = processName;
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}
