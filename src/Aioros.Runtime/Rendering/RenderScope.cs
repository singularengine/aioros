using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

public readonly record struct RenderScope(object? Current, ImmutableArray<object?> Models)
{
    /// <summary>The path of the request being rendered, which marks the links to the current page.</summary>
    public string? Path { get; init; }

    /// <summary>The scopes around the current one, nearest first.</summary>
    public ImmutableStack<object?> Outer { get; init; } = [];

    /// <summary>
    /// Whether nested scopes keep the ones around them; false for a plan none of whose slots reads an outer scope, so
    /// its repeats and nested bodies push nothing.
    /// </summary>
    public bool Tracked { get; init; } = true;

    /// <summary>The scope of a nested body; the current scope becomes the nearest outer one, unless it is the same.</summary>
    public RenderScope With(object? current) =>
        ReferenceEquals(current, Current) ? this : this with { Current = current, Outer = Tracked ? Outer.Push(Current) : Outer };

    /// <summary>The scope around the items of a repeat: the current scope pushed once as the nearest outer one, for all of them.</summary>
    public RenderScope Around() => Tracked ? this with { Outer = Outer.Push(Current) } : this;

    /// <summary>The scope of one item of a repeat written in <paramref name="host"/>, sharing this scope's outer ones; as <see cref="With"/>.</summary>
    public RenderScope Item(RenderScope host, object? item) =>
        ReferenceEquals(item, host.Current) ? host : this with { Current = item };
}
