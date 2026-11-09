using Avalonia;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The data contexts a plan reads from while it is recorded, innermost first: the page's model, each item, each view's
/// model behind the boundary that keeps a view from reading its host, and the type a null scope's member declares.
/// </summary>
internal sealed class PlanScopes
{
    private static readonly object Boundary = new();

    private readonly Stack<object?> _scopes = new();
    private readonly Stack<Type?> _declared = new();

    public PlanScopes(object? root)
    {
        _scopes.Push(root);
        _declared.Push(null);
    }

    public object? Current => _scopes.Peek();

    /// <summary>Starts a scope, with the type its member declares when the scope is null, so the plan can tell the types it reads.</summary>
    public void Push(object? scope, Type? declared)
    {
        _scopes.Push(scope);
        _declared.Push(scope is null ? declared : null);
    }

    public void Pop()
    {
        _scopes.Pop();
        _declared.Pop();
    }

    /// <summary>Starts a view's body: its model is the scope, and no binding inside it may read a scope of its host.</summary>
    public void PushView(object? scope)
    {
        _scopes.Push(Boundary);
        _scopes.Push(scope);
        _declared.Push(null);
    }

    public void PopView()
    {
        _scopes.Pop();
        _scopes.Pop();
        _declared.Pop();
    }

    /// <summary>Whether some scope, on either side of a view's boundary, is this data context.</summary>
    public bool Holds(object? context) => _scopes.Any(scope => ReferenceEquals(scope, context));

    public bool InScope(AvaloniaObject target) => target is not StyledElement element || ReferenceEquals(element.DataContext, Current);

    /// <summary>
    /// How many plan scopes above the current one holds this data context, counting each scope once, or <c>-1</c> when it
    /// is held only beyond the boundary of the view being written.
    /// </summary>
    public int? Depth(object? context)
    {
        var depth = 0;
        object? previous = null;
        foreach (var frame in _scopes)
        {
            if (ReferenceEquals(frame, Boundary))
            {
                return _scopes.Any(outer => ReferenceEquals(outer, context)) ? -1 : null;
            }

            if (depth > 0 && ReferenceEquals(frame, previous))
            {
                continue;
            }

            if (ReferenceEquals(frame, context))
            {
                return depth;
            }

            previous = frame;
            depth++;
        }

        return null;
    }

    /// <summary>The type of the current scope: its value's, or the type declared for it while it holds no value.</summary>
    public Type? ScopeType => Current?.GetType() ?? _declared.Peek();

    /// <summary>The declared type of the member a path of the current scope reads, or nothing when the plan cannot tell it.</summary>
    public Type? PathType(string path)
    {
        if (path.Length == 0 || path.StartsWith('^') || (Current?.GetType() ?? _declared.Peek()) is not { } type)
        {
            return null;
        }

        foreach (var step in Aioros.Runtime.Slots.MemberPaths.Steps(path))
        {
            type = Aioros.Runtime.Slots.MemberPaths.IsIndex(step, out _) ? Element(type) : type.GetProperty(step)?.PropertyType ?? type.GetField(step)?.FieldType;
            if (type is null)
            {
                return null;
            }
        }

        return type;
    }

    private static Type? Element(Type type) =>
        type.IsArray ? type.GetElementType()
        : type.GetInterfaces().Prepend(type).FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))?.GenericTypeArguments[0];
}
