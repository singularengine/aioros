namespace Aioros.Runtime.Rendering;

/// <summary>
/// A plan met data unlike any it learned when the page was compiled (an item deeper than the levels it learned, a value it
/// learned no look for): it cannot write what Avalonia would, so the page is rendered by the engine instead, as Next.js
/// bails a static render out to a dynamic one when it meets what it could not know at build time.
/// </summary>
public sealed class PlanMissException : Exception
{
    public PlanMissException()
    {
    }

    public PlanMissException(string message)
        : base(message)
    {
    }

    public PlanMissException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
