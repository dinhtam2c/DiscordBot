using Application.Abstractions;
using System.Diagnostics;

namespace Infrastructure.Clock;

internal class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeSpan Uptime => _stopwatch.Elapsed;
}
