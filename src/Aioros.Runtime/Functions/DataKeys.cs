using System.Collections.Immutable;
using Aioros.Functions;

namespace Aioros.Runtime.Functions;

/// <summary>
/// What one command changed: the keys its server functions invalidated, and whether it called one without key
/// information. The command's flow carries it (<see cref="Current"/>), as Next.js keeps an action's revalidated tags in
/// its request's async storage; calls running in parallel publish into it without a lock.
/// </summary>
public sealed class DataKeys : IDataKeys
{
    private static readonly AsyncLocal<DataKeys?> Running = new();

    private ImmutableHashSet<string> _invalidated = ImmutableHashSet.Create<string>(StringComparer.Ordinal);

    private int _unknown;

    /// <summary>The changes of the command running in this flow, if one runs.</summary>
    public static DataKeys? Current => Running.Value;

    /// <summary>The <see cref="IDataKeys"/> of the app's services: it records into the command running in the caller's flow.</summary>
    public static IDataKeys Ambient { get; } = new AmbientKeys();

    /// <summary>Starts recording what the flow's command changes; the recording ends with the async method that began it.</summary>
    public static DataKeys Begin()
    {
        var keys = new DataKeys();
        Running.Value = keys;
        return keys;
    }

    /// <summary>Stops recording in this flow, so what runs after the command (its revalidation) records nothing.</summary>
    public static void End() => Running.Value = null;

    public ImmutableHashSet<string> Invalidated => Volatile.Read(ref _invalidated);

    /// <summary>Whether the command called a server function that declares no keys: the whole page must refresh.</summary>
    public bool Unknown => Volatile.Read(ref _unknown) != 0;

    public void Read(string key)
    {
    }

    public void Invalidate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ImmutableInterlocked.Update(ref _invalidated, static (keys, key) => keys.Add(key), key);
    }

    /// <summary>Records a call of a server function without key information.</summary>
    public void Undeclared() => Volatile.Write(ref _unknown, 1);

    private sealed class AmbientKeys : IDataKeys
    {
        public void Read(string key) => Current?.Read(key);

        public void Invalidate(string key) => Current?.Invalidate(key);
    }
}

/// <summary>
/// A generated in-process caller whose reads are recorded into the loader that resolved it: the loader's services hand
/// it their keys when they create it, so a loader that reads no key allocates nothing for keys.
/// </summary>
public interface IKeyedCalls
{
    IDataKeys? Keys { get; set; }
}

/// <summary>
/// The memo of the page load a generated in-process caller runs in: a <c>[Memo]</c> function's equal calls share one run
/// and its result there, as React's <c>cache</c> shares them within one server request. A caller outside a load (a
/// command's) has none and calls the implementation.
/// </summary>
public interface ICallMemo
{
    Task<TResult> CallAsync<TResult>(string function, object arguments, Func<Task<TResult>> call);
}
