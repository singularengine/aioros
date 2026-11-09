using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The page's own state for the request being answered (decision "Guaranteed scope, closed per control type"): the values a post
/// gives the inputs no binding writes back, which the plan renders as the engine's controls show them after the same post.
/// The web target opens one for each post it answers, so the binding of the post and the render of its answer, on that
/// request's flow, share it; without one, every input renders the value the trace saw.
/// </summary>
public static class LocalValues
{
    private static readonly AsyncLocal<State?> Ambient = new();

    public static IDisposable Use()
    {
        var outer = Ambient.Value;
        Ambient.Value = new State();
        return new Restore(outer);
    }

    /// <summary>Whether the request is answered with a state of its own, which a post's inputs can be written into.</summary>
    public static bool InEffect => Ambient.Value is not null;

    public static bool TryGet(int key, out object? value)
    {
        value = null;
        return Ambient.Value is { } state && state.Values.TryGetValue(key, out value);
    }

    public static void Set(int key, object? value)
    {
        if (Ambient.Value is { } state)
        {
            state.Values = state.Values.SetItem(key, value);
        }
    }

    private sealed class State
    {
        public ImmutableDictionary<int, object?> Values { get; set; } = ImmutableDictionary<int, object?>.Empty;
    }

    private sealed class Restore(State? outer) : IDisposable
    {
        public void Dispose() => Ambient.Value = outer;
    }
}
