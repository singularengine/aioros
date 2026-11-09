using System.Collections.Immutable;
using Good.Abstractions;

namespace Good.Feature;

public sealed class SystemClock(TimeProvider time) : IClock
{
    private ImmutableArray<DateTimeOffset> _reads = [];

    public DateTimeOffset Now
    {
        get
        {
            var now = time.GetUtcNow();
            ImmutableInterlocked.Update(ref _reads, reads => reads.Add(now));
            return now;
        }
    }
}

public static class Clocks
{
    public static IClock System { get; } = new SystemClock(TimeProvider.System);
}

public sealed record Tick(DateTimeOffset At);

public readonly record struct Offset(int Minutes);
