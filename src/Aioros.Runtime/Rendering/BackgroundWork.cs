namespace Aioros.Runtime.Rendering;

/// <summary>
/// Work the framework does on its own behalf, such as the startup warm-up that compiles every page in each culture and
/// theme, marked on its flow so the engine runs a request's jobs before it: a visitor waits at most for the background job
/// already running, never for the whole warm-up.
/// </summary>
public static class BackgroundWork
{
    private static readonly AsyncLocal<bool> Marked = new();

    /// <summary>Whether the current flow is background work.</summary>
    public static bool IsRunning => Marked.Value;

    /// <summary>Runs <paramref name="work"/> as background work: every engine job it starts yields to requests' jobs.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Marked.Value = true;
        return await work();
    }
}
