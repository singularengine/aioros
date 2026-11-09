using System.Collections.Immutable;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// Records a page's plan while the writers write it: literal text, and a slot at every value that comes from a binding
/// the plan follows from its scopes (<see cref="PlanScopes"/>, <see cref="BindingPaths"/>), or the reason the plan cannot.
/// Outside a recording it writes every value as it is now.
/// </summary>
internal sealed class PlanRecorder
{
    private readonly PlanBuilder _builder = new();
    private readonly PlanScopes _scopes;
    private readonly FollowedBindings _followed;
    private readonly BindingPaths _paths;
    private readonly HashSet<string> _popovers = new(StringComparer.Ordinal);
    private readonly bool _planning;
    private int _constant;

    /// <summary>The length from which a literal of a render that keeps no plan is a node of its own instead of a copy into the text before it.</summary>
    private const int ApartLength = 16 * 1024;

    public PlanRecorder(bool planning, object? root)
    {
        _planning = planning;
        _scopes = new PlanScopes(root);
        _followed = new FollowedBindings(LocalPath);
        _paths = new BindingPaths(_scopes, _followed, Fail, LocalPath);
    }

    /// <summary>Whether bindings become slots: true while a plan is recorded, outside the parts that only read constants.</summary>
    public bool Planning => _planning && _constant == 0;

    /// <summary>Whether a plan is being recorded, constant parts included.</summary>
    public bool Recording => _planning;

    public string? Failure { get; private set; }

    /// <summary>What the page's author should know about its plan, though it works.</summary>
    public ImmutableArray<string> Warnings => [.. _warnings];

    private readonly HashSet<string> _warnings = new(StringComparer.Ordinal);

    /// <summary>Notes something the page's author should know, once for a page.</summary>
    public void Warn(string warning) => _warnings.Add(warning);

    /// <summary>Where in the XAML the element being written when the plan failed is (<see cref="SourcePositions"/>).</summary>
    public SourcePosition? FailureAt { get; private set; }

    public object? Scope => _scopes.Current;

    public void Literal(string text)
    {
        var mark = text.IndexOf(PopoverMarks.Open);
        if (mark < 0 && !_planning && text.Length >= ApartLength)
        {
            _builder.LiteralApart(text);
            return;
        }

        if (mark < 0)
        {
            _builder.Literal(text);
            return;
        }

        var end = text.IndexOf(PopoverMarks.Close, mark);
        _builder.Literal(text.AsSpan(0, mark));
        var key = text[(mark + 1)..end];
        _builder.Add(new PopoverNode(key, _popovers.Add(key)));
        Literal(text[(end + 1)..]);
    }

    /// <summary>The popovers whose ids are reserved so far, for a write whose output is dropped again (<see cref="ForgetPopovers"/>).</summary>
    public string[] Popovers() => [.. _popovers];

    /// <summary>Forgets the popovers reserved since <see cref="Popovers"/> gave these, so they are reserved again where they next appear.</summary>
    public void ForgetPopovers(string[] kept) => _popovers.IntersectWith(kept);

    public void Text(string? text) => _builder.Literal(CssValues.Encode(text));

    public void Id(SlotSpec? field = null, string? key = null, bool button = false) => _builder.Add(field is null && key is null && !button ? IdNode.Instance : new IdNode { Field = field, Key = key, Button = button });

    public void Add(IPlanNode node) => _builder.Add(node);

    public void Value(AvaloniaObject target, AvaloniaProperty property, ValueFormat format = ValueFormat.Text)
    {
        if (Planning && ValueSlots.Write(this, target, property, format))
        {
            return;
        }

        var text = ValueFormats.Apply(format, target.GetValue(property));
        if (format == ValueFormat.Icon)
        {
            Literal(text);
        }
        else
        {
            Text(text);
        }
    }

    public Slot? BoundFlag(AvaloniaObject target, AvaloniaProperty property) => ValueSlots.Flag(this, target, property);

    /// <summary>
    /// The path of a control's bound <c>DataContext</c>, read from its parent's data context, which must be the current
    /// scope; the control and its descendants then read the value at that path.
    /// </summary>
    public string? ContextPath(Control control) => _paths.ContextPath(control);

    /// <summary>The path of a binding that negates it (<c>{Binding !Path}</c>), followed, or nothing for any other binding.</summary>
    public string? NegatedPath(AvaloniaObject target, AvaloniaProperty property) => _paths.NegatedPath(target, property);

    public string? BoundPath(AvaloniaObject target, AvaloniaProperty property) => _paths.BoundPath(target, property) ?? LocalPath(target, property);

    /// <summary>The bound path without its converter, for the slots that run the converter themselves.</summary>
    public string? RawBoundPath(AvaloniaObject target, AvaloniaProperty property) => _paths.RawBoundPath(target, property) ?? LocalPath(target, property);

    public string? PathOf(AvaloniaObject target, AvaloniaProperty property) => Planning ? _paths.PathOf(target, property) ?? LocalPath(target, property) : null;

    /// <summary>The page's local inputs and their keys (<see cref="Forms.LocalInputs"/>), whose value properties the plan reads from the page's own state.</summary>
    public ImmutableDictionary<AvaloniaObject, Forms.LocalInput> Locals { get; init; } = ImmutableDictionary<AvaloniaObject, Forms.LocalInput>.Empty;

    private string? LocalPath(AvaloniaObject target, AvaloniaProperty property) =>
        Planning && Locals.TryGetValue(target, out var local) && target is Control control && Forms.LocalInputs.Value(control) == property
            ? Aioros.Runtime.Slots.LocalPaths.Of(local.Key, property.PropertyType, target.GetValue(property), local.Guaranteed)
            : null;

    /// <inheritdoc cref="BindingPaths.AncestorPath"/>
    public string? AncestorPath(AvaloniaObject target, string description) => _paths.AncestorPath(target, description);

    /// <inheritdoc cref="BindingPaths.TemplateShown"/>
    public (StyledElement Parent, AvaloniaProperty Property)? TemplateShown(AvaloniaObject target, AvaloniaProperty property) => _paths.TemplateShown(target, property);

    public bool InScope(AvaloniaObject target) => _scopes.InScope(target);

    /// <inheritdoc cref="PlanScopes.PathType"/>
    public Type? PathType(string path) => _scopes.PathType(path);

    /// <inheritdoc cref="FollowedBindings.Follow"/>
    public void Follow(AvaloniaObject target, AvaloniaProperty property) => _followed.Follow(target, property);

    /// <inheritdoc cref="FollowedBindings.IsFollowed"/>
    public bool IsFollowed(AvaloniaObject target, AvaloniaProperty property) => _followed.IsFollowed(target, property);

    /// <inheritdoc cref="FollowedBindings.Presented"/>
    public void Presented(AvaloniaObject presenter, AvaloniaProperty property) => _followed.Presented(presenter, property);

    /// <inheritdoc cref="FollowedBindings.Learning"/>
    public IDisposable Learning(AvaloniaObject target, AvaloniaProperty property) => _followed.Learning(target, property);

    /// <inheritdoc cref="FollowedBindings.ReadsModel"/>
    public bool ReadsModel(AvaloniaObject target, AvaloniaProperty property) => _followed.ReadsModel(target, property);

    /// <inheritdoc cref="FollowedBindings.FollowsConstantElement"/>
    public bool FollowsConstantElement(AvaloniaObject target, AvaloniaProperty property) => _followed.FollowsConstantElement(target, property);

    /// <inheritdoc cref="FollowedBindings.Written"/>
    public void Written(Control control) => _followed.Written(control);

    /// <summary>
    /// Fails the plan on the first binding of a written control (or of an object it holds, such as a transform) that no
    /// slot reads: such a value would be frozen at the compiled model's.
    /// </summary>
    public void FailOnUnfollowed()
    {
        if (_followed.Unfollowed() is var (reason, control))
        {
            FailAt(reason, control);
        }
    }

    /// <summary>
    /// How a templated control's template reads its property: whether some part binds it, and whether every binding of a
    /// part that reads it (alone or in a <c>MultiBinding</c>) is a followed slot.
    /// </summary>
    public (bool Read, bool Slotted) TemplateReads(StyledElement control, AvaloniaProperty property)
    {
        var readers = Readers(control, property);
        return (readers.Count > 0, readers.All(reader => _followed.IsFollowed(reader.Part, reader.Read) && (TemplateShown(reader.Part, reader.Read) is not null || _followed.IsPresented(reader.Part, reader.Read))));
    }

    /// <summary>Whether some part of the control's template binds the property, and a slot follows every such binding, through a converter or not.</summary>
    public bool TemplateFollows(StyledElement control, AvaloniaProperty property) =>
        Readers(control, property) is { Count: > 0 } readers && readers.All(reader => _followed.IsFollowed(reader.Part, reader.Read));

    private static List<(StyledElement Part, AvaloniaProperty Read)> Readers(StyledElement control, AvaloniaProperty property) =>
        [.. ((Visual)control).GetVisualDescendants().OfType<StyledElement>().Where(part => ReferenceEquals(part.TemplatedParent, control))
            .SelectMany(part => AvaloniaPropertyRegistry.Instance.GetRegistered(part).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(part.GetType()))
                .Where(read => BindingOperations.GetBindingExpressionBase(part, read) is { } expression && TemplateBindings.Reads(expression, control, property))
                .Select(read => (part, read)))];

    public void Begin(object? scope) => Begin(scope, null);

    /// <summary>Starts a scope, with the type its member declares when the scope is null, so the plan can tell the types it reads.</summary>
    public void Begin(object? scope, Type? declared)
    {
        _builder.Begin();
        _scopes.Push(scope, declared);
    }

    public ImmutableArray<IPlanNode> End()
    {
        _scopes.Pop();
        return _builder.End();
    }

    /// <summary>Starts a view's body: its model is the scope, and no binding inside it may read a scope of its host.</summary>
    public void BeginView(object? scope)
    {
        _builder.Begin();
        _scopes.PushView(scope);
    }

    public ImmutableArray<IPlanNode> EndView()
    {
        _scopes.PopView();
        return _builder.End();
    }

    public void Fail(string reason) => FailAt(reason, _writing.Count == 0 ? null : _writing.Peek());

    private void FailAt(string reason, AvaloniaObject? element)
    {
        if (Failure is null)
        {
            Failure = reason;
            FailureAt = SourcePositions.Of(element, _writing);
        }
    }

    private readonly Stack<AvaloniaObject> _writing = new();

    /// <summary>Notes the control being written, so a failure while it is written names its place in the XAML, until <see cref="DoneWriting"/>.</summary>
    public void Writing(Control control) => _writing.Push(control);

    public void DoneWriting() => _writing.Pop();

    /// <summary>Lets the bindings of an element's template read its properties while its writer computes what they give for each render.</summary>
    public IDisposable Computing(AvaloniaObject element)
    {
        _paths.Computed.Add(element);
        return new Undo(() => _paths.Computed.Remove(element));
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    /// <summary>Runs a write the plan may still take back, and gives the reason it failed, if it did, without failing the plan.</summary>
    public string? Attempt(Action write)
    {
        var (kept, keptAt) = (Failure, FailureAt);
        Failure = null;
        try
        {
            write();
            return Failure;
        }
        finally
        {
            (Failure, FailureAt) = (kept, keptAt);
        }
    }

    /// <summary>
    /// Writes a part whose data is a constant of the page (content set in the XAML, not bound, with the template its type
    /// takes) as Avalonia renders it, as a resource is: its bindings read that constant and no model, so every render
    /// writes the same. A binding in it that reads the scope's model or another element fails the plan instead.
    /// </summary>
    public void WriteConstant(Visual root, Action write)
    {
        if (!Planning)
        {
            write();
            return;
        }

        foreach (var element in root.GetSelfAndVisualDescendants().OfType<StyledElement>())
        {
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(element.GetType())))
            {
                if (LocalBindings.Of(element, property) is { } binding
                    && ((binding as IDescription)?.Description is not { } path || !ElementBindings.IsSimplePath(path) || _scopes.Holds(element.DataContext)))
                {
                    Fail($"{element.GetType().Name}.{property.Name} is bound in the template of a constant content to '{(binding as IDescription)?.Description}', which reads more than that content");
                    return;
                }
            }
        }

        _constant++;
        try
        {
            write();
        }
        finally
        {
            _constant--;
        }
    }

    public ImmutableArray<IPlanNode> Build() => _builder.Build();
}
