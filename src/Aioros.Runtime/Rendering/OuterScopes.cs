using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The data scopes around the one being rendered, nearest first, for slots that read an ancestor's data context
/// (Avalonia's <c>$parent[...]</c> and <c>#name</c> bindings). A render is synchronous, so the scopes are published to
/// the getters it calls on the rendering thread while each body is written.
/// </summary>
public static class OuterScopes
{
    [ThreadStatic]
    private static ImmutableStack<object?>? _current;

    public static ImmutableStack<object?> Current => _current ?? [];

    /// <summary>The scope <paramref name="depth"/> levels above the current one.</summary>
    public static object? At(int depth)
    {
        var scopes = Current;
        for (var level = 1; level < depth && !scopes.IsEmpty; level++)
        {
            scopes = scopes.Pop();
        }

        return scopes.IsEmpty ? null : scopes.Peek();
    }

    /// <summary>Publishes the scopes and returns the ones they replace, to publish again when the body is written.</summary>
    public static ImmutableStack<object?>? Swap(ImmutableStack<object?>? scopes)
    {
        var previous = _current;
        _current = scopes;
        return previous;
    }
}
