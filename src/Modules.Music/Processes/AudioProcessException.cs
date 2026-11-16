namespace Modules.Music.Processes;

internal sealed class AudioProcessException(string processName, int exitCode, string stderr)
    : Exception(string.IsNullOrWhiteSpace(stderr)
        ? $"Process '{processName}' exited with code {exitCode}."
        : $"Process '{processName}' exited with code {exitCode}. Standard error: {stderr.Trim()}")
{
    public string ProcessName { get; } = processName;
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;

    public static bool IsTransientFailure(Exception error)
        => error is TimeoutException || error switch
        {
            AudioProcessException process => process.IsTransient,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsTransientFailure),
            _ => error.InnerException is { } inner && IsTransientFailure(inner),
        };

    public static AudioProcessException? Find(Exception error)
        => error switch
        {
            AudioProcessException process => process,
            AggregateException aggregate => aggregate.InnerExceptions.Select(Find).FirstOrDefault(process => process is not null),
            _ => error.InnerException is { } inner ? Find(inner) : null,
        };

    public Application.Abstractions.UserFriendlyException ForUser()
    {
        var message = Stderr.Contains("Sign in to confirm your age", StringComparison.OrdinalIgnoreCase)
            ? "This song can't be played because YouTube requires age verification. Try another song."
            : Stderr.Contains("private video", StringComparison.OrdinalIgnoreCase)
                || Stderr.Contains("private track", StringComparison.OrdinalIgnoreCase)
            ? "This song is private and can't be played. Try another song."
            : !IsTransient && (Stderr.Contains("not available", StringComparison.OrdinalIgnoreCase)
                || Stderr.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                || Stderr.Contains("has been removed", StringComparison.OrdinalIgnoreCase))
            ? "This song is unavailable. Try another song."
            : Stderr.Contains("sign in", StringComparison.OrdinalIgnoreCase)
                || Stderr.Contains("login required", StringComparison.OrdinalIgnoreCase)
            ? "This song can't be played because the provider requires signing in. Try another song."
            : IsTransient
            ? "This song couldn't be loaded because of a network error or a temporary access restriction. Please try again later."
            : "This song couldn't be read from the audio provider. Try another song.";
        return new(message, this);
    }

    // Retry transport failures, not unavailable songs, authentication requirements or decoder errors.
    public bool IsTransient => !new[] { "sign in", "login required", "private video", "private track",
            "video unavailable", "video is not available", "has been removed", "not available in your country" }
        .Any(message => Stderr.Contains(message, StringComparison.OrdinalIgnoreCase))
        && (System.Text.RegularExpressions.Regex.IsMatch(Stderr,
            @"\b(?:HTTP(?: Error)?|Server returned)\s+(?:403|408|429|5\d\d)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        || new[] { "connection reset", "connection refused", "connection timed out", "timed out",
            "temporary failure in name resolution", "network is unreachable", "input/output error",
            "getaddrinfo failed", "failed to resolve hostname", "could not resolve host",
            "remote end closed connection", "connection closed prematurely", "tls error" }
            .Any(message => Stderr.Contains(message, StringComparison.OrdinalIgnoreCase)));
}
