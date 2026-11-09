using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

using Aioros.Runtime.Slots;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The paths a plan reads bound values through, from the scope that holds the data context each binding reads: paths of
/// the current scope, of an outer scope (<c>^depth.path</c>), through another element or a template, or through an explicit
/// source. A binding the plan cannot follow fails the plan with its reason.
/// </summary>
internal sealed class BindingPaths
{
    private readonly PlanScopes _scopes;
    private readonly FollowedBindings _followed;
    private readonly Action<string> _fail;
    private readonly Func<AvaloniaObject, AvaloniaProperty, string?> _local;

    public BindingPaths(PlanScopes scopes, FollowedBindings followed, Action<string> fail, Func<AvaloniaObject, AvaloniaProperty, string?> local)
    {
        _scopes = scopes;
        _followed = followed;
        _fail = fail;
        _local = local;
    }

    /// <summary>
    /// The path of a control's bound <c>DataContext</c>, read from its parent's data context, which must be the current
    /// scope; the control and its descendants then read the value at that path.
    /// </summary>
    public string? ContextPath(Control control)
    {
        if (LocalBindings.Of(control, StyledElement.DataContextProperty) is not { } binding)
        {
            return null;
        }

        var path = (binding as IDescription)?.Description;
        if (BindingSources.Explicit(binding, control, out var source) && !ReferenceEquals(source, _scopes.Current))
        {
            _fail(ThroughSource(control, StyledElement.DataContextProperty, source));
            return null;
        }

        if (!Indexes(control, StyledElement.DataContextProperty, binding, ref path))
        {
            return null;
        }

        if (path is null || !ElementBindings.IsSimplePath(path) || control.Parent is not { } parent || !ReferenceEquals(parent.DataContext, _scopes.Current))
        {
            _fail($"{control.GetType().Name}.DataContext is bound to '{path}', which the plan cannot follow");
            return null;
        }

        _followed.Follow(control, StyledElement.DataContextProperty);
        return path;
    }

    /// <summary>The path of a binding that negates it (<c>{Binding !Path}</c>), followed, or nothing for any other binding.</summary>
    public string? NegatedPath(AvaloniaObject target, AvaloniaProperty property)
    {
        if (LocalBindings.Of(target, property) is not { } binding || binding is not IDescription { Description: ['!', .. var negated] })
        {
            return null;
        }

        if (!ElementBindings.IsSimplePath(negated))
        {
            return AncestorPath(target, negated) is { } ancestor ? Followed(target, property, ancestor) : null;
        }

        if (Sourced(target, property, binding, negated, out var sourced))
        {
            return sourced;
        }

        if (!CurrentScope(target, property))
        {
            return null;
        }

        return Streamed(target, property, negated);
    }

    private bool CurrentScope(AvaloniaObject target, AvaloniaProperty property)
    {
        if (target is StyledElement element && !ReferenceEquals(element.DataContext, _scopes.Current))
        {
            if (ConstantBindings.ConstantItem(element))
            {
                _followed.Follow(target, property);
                return false;
            }

            _fail($"{target.GetType().Name}.{property.Name}: the data context is not the current scope");
            return false;
        }

        return true;
    }

    /// <summary>The bound path, read through its binding's converter when it has one (<see cref="ConvertedPaths"/>).</summary>
    public string? BoundPath(AvaloniaObject target, AvaloniaProperty property) => Converted(target, property, RawBoundPath(target, property));

    public string? RawBoundPath(AvaloniaObject target, AvaloniaProperty property)
    {
        if (LocalBindings.Of(target, property) is not { } expression)
        {
            return null;
        }

        var path = (expression as IDescription)?.Description;
        if (Sourced(target, property, expression, path, out var sourced))
        {
            return sourced;
        }

        if (!Read(target, property, expression, ref path))
        {
            return null;
        }

        if (path is not null && !ElementBindings.IsSimplePath(path) && AncestorPath(target, path) is { } ancestor)
        {
            _followed.Follow(target, property);
            return ancestor;
        }

        if (path is not null && !ElementBindings.IsSimplePath(path) && (ConstantElement(target, property, path) || ConstantTemplated(target, property, path)))
        {
            return null;
        }

        if (path is null || !ElementBindings.IsSimplePath(path))
        {
            _fail($"{target.GetType().Name}.{property.Name} is bound to '{path}'");
            return null;
        }

        if (target is StyledElement element && !ReferenceEquals(element.DataContext, _scopes.Current))
        {
            if (ConstantBindings.ConstantItem(element))
            {
                _followed.Follow(target, property);
                return null;
            }

            _fail($"{target.GetType().Name}.{property.Name}: the data context is not the current scope");
            return null;
        }

        return Streamed(target, property, path);
    }

    /// <summary>
    /// Writes into the path the integer indexes of the lists it reads (<see cref="IndexedPaths"/>); false, failing the plan,
    /// when it reads through an indexer that is not one.
    /// </summary>
    private bool Indexes(AvaloniaObject target, AvaloniaProperty property, BindingExpressionBase binding, ref string? path)
    {
        if (path is null || IndexedPaths.Restore(path, _scopes.ScopeType, binding) is not { } restored)
        {
            if (path is not null)
            {
                _fail($"{target.GetType().Name}.{property.Name} is bound through an indexer ('{path}') that is not an integer index of a list, which the plan cannot follow");
            }

            return path is null;
        }

        path = restored;
        return true;
    }

    /// <summary>
    /// Follows a path from the current scope, unless it reads a stream (<c>Title^</c>) that gives no value when
    /// subscribed: Avalonia then leaves the property at its default and runs no converter, which a slot cannot tell
    /// from a null value.
    /// </summary>
    private string? Streamed(AvaloniaObject target, AvaloniaProperty property, string path)
    {
        if (path.Contains('^', StringComparison.Ordinal) && StreamValues.Silent(path, _scopes.Current) is { } silent)
        {
            _fail($"{target.GetType().Name}.{property.Name} is bound to '{silent}', a stream that gives no value when subscribed, which the plan cannot follow");
            return null;
        }

        _followed.Follow(target, property);
        return path;
    }

    /// <summary>Whether the target reads the current scope, so its paths are relative to it.</summary>
    private string Followed(AvaloniaObject target, AvaloniaProperty property, string path)
    {
        _followed.Follow(target, property);
        return path;
    }

    public string? PathOf(AvaloniaObject target, AvaloniaProperty property) => Converted(target, property, RawPathOf(target, property));

    /// <summary>The path read through the binding's converter when it has one the plan can rebuild, the path as it is otherwise.</summary>
    private static string? Converted(AvaloniaObject target, AvaloniaProperty property, string? path) =>
        path is not null && BoundConverters.Plain(property.PropertyType) && LocalBindings.Of(target, property) is { } binding && BoundConverters.Spec(binding, path, property.PropertyType) is { } convert
            ? ConvertedPaths.Of(convert)
            : path;

    private string? RawPathOf(AvaloniaObject target, AvaloniaProperty property)
    {
        if (LocalBindings.Of(target, property) is not { } binding || binding is not IDescription { Description: { } path })
        {
            return null;
        }

        if (Sourced(target, property, binding, path, out var sourced))
        {
            return sourced;
        }

        if (target is StyledElement element && !ReferenceEquals(element.DataContext, _scopes.Current))
        {
            return null;
        }

        if (IndexedPaths.Restore(path, _scopes.ScopeType, binding) is not { } restored)
        {
            return null;
        }

        path = restored;
        if (ElementBindings.IsSimplePath(path))
        {
            return Streamed(target, property, path);
        }

        var followed = AncestorPath(target, path);
        if (followed is not null)
        {
            _followed.Follow(target, property);
        }

        return followed;
    }

    /// <summary>
    /// The path of a binding with an explicit <c>Source</c> that is a data context the plan holds, from the scope that
    /// holds it; any other source the plan cannot follow fails the plan. False when the binding reads its data context.
    /// </summary>
    private bool Sourced(AvaloniaObject target, AvaloniaProperty property, BindingExpressionBase binding, string? path, out string? sourced)
    {
        sourced = null;
        if (!BindingSources.Explicit(binding, target, out var source))
        {
            return false;
        }

        if (FromElement(target, property, source, path, out sourced))
        {
            return true;
        }

        var depth = source is AvaloniaObject || path is null || !ElementBindings.IsSimplePath(path) ? null : _scopes.Depth(source);
        if (depth is not { } scope || scope < 0)
        {
            _fail(ThroughSource(target, property, source, depth == -1 ? "the data context of its view's host" : path is null || ElementBindings.IsSimplePath(path) ? null : $"the path '{path}'"));
            return true;
        }

        _followed.Follow(target, property);
        sourced = scope == 0 ? path : path!.Length == 0 ? $"^{scope}" : $"^{scope}.{path}";
        return true;
    }

    /// <summary>Whether a binding's <c>Source</c> is an element the plan reads (its property's path, or a constant of the page), followed.</summary>
    private bool FromElement(AvaloniaObject target, AvaloniaProperty property, object? source, string? path, out string? sourced)
    {
        sourced = SourcedElement(target, source, path);
        if (sourced is null && !ConstantSource(source, path))
        {
            return false;
        }

        _followed.Follow(target, property);
        return true;
    }

    /// <summary>The path of a binding whose <c>Source</c> is an element, read as <c>#name.Property</c> reads it, or nothing.</summary>
    private string? SourcedElement(AvaloniaObject target, object? source, string? path)
    {
        if (source is not StyledElement element || path is not { Length: > 0 } || !ElementBindings.IsSimplePath(path))
        {
            return null;
        }

        var dot = path.IndexOf('.', StringComparison.Ordinal);
        return ElementPath(target, element, dot < 0 ? path : path[..dot], dot < 0 ? string.Empty : path[(dot + 1)..], path);
    }

    /// <summary>Whether a binding's <c>Source</c> is an element whose property it reads holds a constant of the page: not bound, and not a local input's value.</summary>
    private bool ConstantSource(object? source, string? path) =>
        source is StyledElement element && path is not null && Registered(element, path) is { } read && LocalBindings.Of(element, read) is null && _local(element, read) is null;

    private static string ThroughSource(AvaloniaObject target, AvaloniaProperty property, object? source, string? detail = null) =>
        $"{target.GetType().Name}.{property.Name} is bound through Source to {(source is AvaloniaObject ? "the element" : "the object")} {source?.GetType().Name ?? "null"}"
        + $" ({detail ?? (source is AvaloniaObject ? "bind to it with #name or $parent" : "a data context no scope of the plan holds")}), which the plan cannot follow";

    /// <summary>
    /// The path of a binding to another element (<c>$parent[Type]</c>, <c>$parent[Type;level]</c>,
    /// <c>$visualParent[...]</c> or <c>#name</c>): to its <c>DataContext</c> and a member path, or to one of its
    /// properties that is itself bound to a path, or the value of a local input (<see cref="Forms.LocalInputs"/>). The result is a path from the plan scope that holds the data context
    /// it reads: <c>^depth.path</c> for an outer scope, or nothing when no scope holds it.
    /// </summary>
    public string? AncestorPath(AvaloniaObject target, string description)
    {
        if (target is not StyledElement element || ElementBindings.Parse(description) is not { Success: true } match
            || ElementBindings.Source(element, match) is not { } source)
        {
            return null;
        }

        return ElementPath(target, source, match.Groups["property"].Success ? match.Groups["property"].Value : null, match.Groups["rest"].Value, description);
    }

    /// <summary>The path of a binding that reads another element: the value of a local input, a property of it bound to a path, or its data context and a member path, from the plan scope that holds that data context.</summary>
    private string? ElementPath(AvaloniaObject target, StyledElement source, string? name, string tail, string description)
    {
        if (name is not null && tail.Length == 0 && Registered(source, name) is { } read && _local(source, read) is { } local)
        {
            return local;
        }

        var rest = name is not null ? BoundThrough(source, name, tail) : tail;
        if (rest is null)
        {
            return null;
        }

        var depth = _scopes.Depth(source.DataContext);
        if (depth == -1)
        {
            _fail($"{target.GetType().Name} is bound to '{description}', which reaches across the boundary of its view into its host; a view reads only its own view model");
            return null;
        }

        return depth is null ? null
            : depth == 0 ? rest
            : rest.Length == 0 ? $"^{depth}"
            : $"^{depth}.{rest}";
    }

    /// <summary>
    /// Whether a binding reads a property of another element (<c>#name.Property</c>, <c>$parent[Type].Property</c>, negated
    /// or not) that the plan writes as rendered: an element with no binding that reads the model holds what its XAML gives
    /// it on every render. An element with such a binding fails the plan.
    /// </summary>
    /// <summary>The elements whose writers compute for each render what their properties give the bindings that read them (a planned calendar's caption selects).</summary>
    public HashSet<AvaloniaObject> Computed { get; } = [];

    private bool Computes(AvaloniaObject target, string path) =>
        target is StyledElement element && ElementBindings.Parse(path.TrimStart('!')) is { Success: true } match && ElementBindings.Source(element, match) is { } source && Computed.Contains(source);

    private bool ConstantElement(AvaloniaObject target, AvaloniaProperty property, string path)
    {
        if (ConstantBindings.ElementModelBinding(target, path, 0) is not { } bound)
        {
            return false;
        }

        if (bound.Length > 0 && !Computes(target, path))
        {
            _fail($"{target.GetType().Name}.{property.Name} is bound to '{path}', a property of an element whose {bound} reads the model");
            return true;
        }

        _followed.Follow(target, property);
        return true;
    }

    /// <summary>
    /// Whether a binding in a control template reads a property of its templated control (<c>$templatedParent.Name</c>,
    /// negated or not) that the control holds as a constant of the page (see <see cref="TemplateBindings.ReadsConstantTemplate"/>), followed.
    /// </summary>
    private bool ConstantTemplated(AvaloniaObject target, AvaloniaProperty property, string path)
    {
        if (!path.TrimStart('!').StartsWith(TemplateBindings.TemplatedParentPrefix, StringComparison.Ordinal) || TemplateBindings.ConstantParent(target) is not { } parent)
        {
            return false;
        }

        var name = path.TrimStart('!')[TemplateBindings.TemplatedParentPrefix.Length..];
        var read = AvaloniaPropertyRegistry.Instance.GetRegistered(parent).FirstOrDefault(candidate => candidate.Name == name);
        if (read is null || !ConstantBindings.ConstantProperty(parent, read))
        {
            return false;
        }

        _followed.Follow(target, property);
        return true;
    }

    /// <summary>
    /// The property of its templated control that a template part shows unchanged (<c>{TemplateBinding Title}</c>,
    /// <c>$templatedParent.Title</c>, no converter, a type the part's property takes as is, such as a list as an items
    /// source), when the control binds it to the model in the current scope: the part then reads the control's binding, as
    /// a slot.
    /// </summary>
    public (StyledElement Parent, AvaloniaProperty Property)? TemplateShown(AvaloniaObject target, AvaloniaProperty property) =>
        TemplateBindings.Bound(target, property) is { } shown && _scopes.InScope(shown.Parent) ? shown : null;

    /// <summary>The path a property of the source element is bound to, followed by the rest of the path, if it is.</summary>
    /// <summary>
    /// The path a binding reads: through the binding of its target's own property it names (<see cref="Own"/>), or with
    /// its indexes written in; false when it reads neither, failing the plan, or when the own property it names is not
    /// bound, a constant of the page its value is written as.
    /// </summary>
    private bool Read(AvaloniaObject target, AvaloniaProperty property, BindingExpressionBase binding, ref string? path)
    {
        if (target is not StyledElement self || Own(target, binding, path) is not { } own)
        {
            return Indexes(target, property, binding, ref path);
        }

        path = BoundThrough(self, own.Name, own.Path);
        if (path is not null)
        {
            return true;
        }

        if (Registered(self, own.Name) is { } named && LocalBindings.Of(self, named) is null)
        {
            _followed.Follow(target, property);
            return false;
        }

        _fail($"{target.GetType().Name}.{property.Name} is bound to its own {own.Name}, which is not bound to a path of the model");
        return false;
    }

    /// <summary>
    /// The property of its target itself a binding reads (<c>$self.Property</c>, or a path under <c>RelativeSource Self</c>,
    /// as a library's code binds one of its control's properties to another), and the path after it; nothing for a
    /// binding that reads anything else.
    /// </summary>
    private static (string Name, string Path)? Own(AvaloniaObject target, BindingExpressionBase binding, string? path)
    {
        var own = path switch
        {
            { } described when described.StartsWith("$self.", StringComparison.Ordinal) => described["$self.".Length..],
            { } described when ElementBindings.IsSimplePath(described) && described.Length > 0 && BindingSources.ReadsSelf(binding, target) => described,
            _ => null,
        };
        if (own is null || !ElementBindings.IsSimplePath(own))
        {
            return null;
        }

        var dot = own.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? (own, string.Empty) : (own[..dot], own[(dot + 1)..]);
    }

    private static AvaloniaProperty? Registered(StyledElement source, string name) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(source).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(source.GetType()))
            .FirstOrDefault(candidate => candidate.Name == name);

    private string? BoundThrough(StyledElement source, string name, string rest)
    {
        var property = Registered(source, name);
        if (property is null || LocalBindings.Of(source, property) is not { } binding || BindingSources.Explicit(binding, source, out _)
            || binding is not IDescription { Description: { } bound } || !ElementBindings.IsSimplePath(bound))
        {
            return null;
        }

        _followed.Follow(source, property);
        return bound.Length == 0 ? rest : rest.Length == 0 ? bound : $"{bound}.{rest}";
    }
}
