using Application.Abstractions;

namespace Infrastructure.Clock;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
