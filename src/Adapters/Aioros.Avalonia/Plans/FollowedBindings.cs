using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The bindings a plan reads into slots, the controls it writes and the properties it learns by value, so that once the
/// page is written every binding of a written control is accounted for (<see cref="Unfollowed"/>).
/// </summary>
internal sealed class FollowedBindings(Func<AvaloniaObject, AvaloniaProperty, string?> local)
{
    private readonly HashSet<(AvaloniaObject, AvaloniaProperty)> _followed = [];
    private readonly HashSet<(AvaloniaObject, AvaloniaProperty)> _presented = [];
    private readonly HashSet<(AvaloniaObject, AvaloniaProperty)> _learning = [];
    private readonly List<Control> _written = [];

    /// <summary>Notes that the plan reads this binding into a slot.</summary>
    public void Follow(AvaloniaObject target, AvaloniaProperty property) => _followed.Add((target, property));

    /// <summary>Whether a slot reads this binding.</summary>
    public bool IsFollowed(AvaloniaObject target, AvaloniaProperty property) => _followed.Contains((target, property));

    /// <summary>Notes a template's presenter that shows its control's bound property in that value's scope (<see cref="Html.Templating.TemplatedContents"/>).</summary>
    public void Presented(AvaloniaObject presenter, AvaloniaProperty property)
    {
        _presented.Add((presenter, property));
        Follow(presenter, property);
    }

    public bool IsPresented(AvaloniaObject presenter, AvaloniaProperty property) => _presented.Contains((presenter, property));

    /// <summary>Marks a property whose values the plan learns, so the writers write each value set above its binding as a constant.</summary>
    public IDisposable Learning(AvaloniaObject target, AvaloniaProperty property)
    {
        _learning.Add((target, property));
        return new Unlearn(_learning, (target, property));
    }

    private sealed class Unlearn(HashSet<(AvaloniaObject, AvaloniaProperty)> learning, (AvaloniaObject, AvaloniaProperty) entry) : IDisposable
    {
        public void Dispose() => learning.Remove(entry);
    }

    /// <summary>Whether the property is bound to the model, rather than to another element that holds a constant of the page.</summary>
    public bool ReadsModel(AvaloniaObject target, AvaloniaProperty property) => LocalBindings.IsBound(target, property) && !_learning.Contains((target, property)) && !FollowsConstantElement(target, property);

    /// <summary>
    /// Whether the property is bound to a property of another element that holds a constant of the page (see
    /// <see cref="ConstantBindings.ElementModelBinding"/>), followed; not the value of a local input
    /// (<see cref="Forms.LocalInputs"/>), which changes with the page's own state and is read through its local path.
    /// </summary>
    public bool FollowsConstantElement(AvaloniaObject target, AvaloniaProperty property)
    {
        if (LocalBindings.Of(target, property) is not IDescription { Description: { } path } || ElementBindings.IsSimplePath(path) || ReadsLocal(target, path) || ConstantBindings.ElementModelBinding(target, path, 0) is not "")
        {
            return false;
        }

        Follow(target, property);
        return true;
    }

    private bool ReadsLocal(AvaloniaObject target, string path) =>
        target is StyledElement element && ElementBindings.Parse(path.TrimStart('!')) is { Success: true } match
        && match.Groups["property"].Success && match.Groups["rest"].Value.Length == 0 && ElementBindings.Source(element, match) is { } source
        && AvaloniaPropertyRegistry.Instance.GetRegistered(source).FirstOrDefault(candidate => candidate.Name == match.Groups["property"].Value) is { } read
        && local(source, read) is not null;

    /// <summary>Notes a control the plan writes, whose bindings must all be followed.</summary>
    public void Written(Control control)
    {
        _written.Add(control);
        if (ConstantBindings.Branching)
        {
            FollowConstantElements(control);
        }
    }

    /// <summary>
    /// Follows a written control's bindings that read a constant of another element while that element's state is what the
    /// plan learns (a tree's container being learned), so the check of unfollowed bindings, made once the page is written
    /// and the container's looks are gone, does not take them for model reads.
    /// </summary>
    private void FollowConstantElements(Control control)
    {
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(control))
        {
            _ = LocalBindings.Of(control, property) is not null && FollowsConstantElement(control, property);
        }
    }

    /// <summary>
    /// The first binding of a written control (or of an object it holds, such as a transform) that no slot reads: such a
    /// value would be frozen at the compiled model's.
    /// </summary>
    public (string Reason, Control Control)? Unfollowed()
    {
        foreach (var control in _written)
        {
            if (Unfollowed(control, 0) is { } reason)
            {
                return (reason, control);
            }
        }

        return null;
    }

    private string? Unfollowed(AvaloniaObject target, int depth)
    {
        bool? constant = null;
        foreach (var property in RegisteredProperties.Of(target.GetType()))
        {
            if (LocalBindings.Of(target, property) is { } binding && !_followed.Contains((target, property)) && !ElementBindings.IsElementReference(binding) && !FollowsConstantElement(target, property))
            {
                return $"{target.GetType().Name}.{property.Name} is bound, and the plan does not follow it";
            }

            if (depth < 3 && !property.IsDirect && !(constant ??= target is StyledElement holder && ConstantBindings.ConstantItem(holder)) && Held(target.GetValue(property)).Select(held => Unfollowed(held, depth + 1)).FirstOrDefault(reason => reason is not null) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    private static IEnumerable<AvaloniaObject> Held(object? value) => value switch
    {
        Visual => [],
        AvaloniaObject held => [held],
        System.Collections.IEnumerable items when value.GetType().Namespace?.StartsWith("Avalonia", StringComparison.Ordinal) == true =>
            items.OfType<AvaloniaObject>().Where(item => item is not Visual),
        _ => [],
    };
}
