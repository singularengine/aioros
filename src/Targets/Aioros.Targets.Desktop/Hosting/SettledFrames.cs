namespace Aioros.Targets.Desktop.Hosting;

/// <summary>
/// The frame a capture keeps: the first that the next <see cref="Stable"/> frames leave unchanged while no animation
/// work is pending, so a page whose enter animations are still running, or still waiting out their begin delay on the
/// same frame, is not caught half way; a page that never settles is kept after <see cref="Limit"/> frames. Settling is
/// read from the frames themselves and from the pending animations, never from a fixed delay.
/// </summary>
public static class SettledFrames
{
    /// <summary>How many frames in a row must show the same pixels.</summary>
    public const int Stable = 5;

    /// <summary>The most frames a capture waits for, about ten seconds at sixty frames a second.</summary>
    public const int Limit = 600;

    /// <summary>
    /// Takes frames, each after <paramref name="next"/> has moved the page on by one, until <see cref="Stable"/> frames in
    /// a row, taken while <paramref name="animating"/> is false, are the same as the one before them, and gives back the
    /// settled one; the frames left behind are disposed.
    /// </summary>
    public static async Task<T?> WaitAsync<T>(Func<T?> capture, Func<T, T, bool> same, Func<bool> animating, Func<Task> next)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(same);
        ArgumentNullException.ThrowIfNull(animating);
        ArgumentNullException.ThrowIfNull(next);
        T? last = null;
        var unchanged = 0;
        for (var frame = 0; frame < Limit && unchanged < Stable; frame++)
        {
            await next();
            if (capture() is not { } current)
            {
                continue;
            }

            if (animating())
            {
                last?.Dispose();
                (last, unchanged) = (current, 0);
                continue;
            }

            if (last is not null && same(last, current))
            {
                unchanged++;
                current.Dispose();
                continue;
            }

            last?.Dispose();
            (last, unchanged) = (current, 0);
        }

        return last;
    }
}
