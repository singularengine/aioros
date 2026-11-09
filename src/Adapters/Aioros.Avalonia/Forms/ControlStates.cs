using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Html;
using Aioros.Markup;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// The state a library's controls carry across posts (<see cref="Web.StateProperty"/>): each named property of their own
/// written in a hidden input <c>_s{n}</c>, numbered in the logical tree's order (hidden controls included, so a step not
/// shown keeps its place), and restored before a post is bound, as a tab control's selected index travels in its hidden
/// input and as a multi-step form carries its step (Remix's hidden inputs, Web Forms' view state). The properties are
/// found through Avalonia's property registry by the names the library gives (<c>Owner.Name</c> for an attached one),
/// never by reflection; a value that does not parse, or a state with no property any more, is left out.
/// </summary>
internal static class ControlStates
{
    public const string Prefix = "_s";

    /// <summary>The name of an overlay's open state, which the browser changes itself and the server does not seal.</summary>
    public const string ModalPrefix = "_m";

    /// <summary>The library's controls and their properties that travel, in the logical tree's order.</summary>
    public static List<(Control Control, AvaloniaProperty Property)> Of(Control root) =>
        [.. All(root).Where(static state => Html.Variants.BrowserModals.Property(state.Control) == state.Property || Html.Templating.ItemIdentity.Around(state.Control) is null)];

    private static IEnumerable<(Control Control, AvaloniaProperty Property)> All(Control root) =>
        root.GetLogicalDescendants().Prepend(root).OfType<Control>()
            .Where(static control => Web.GetState(control) is { Length: > 0 } || Html.Variants.BrowserModals.Property(control) is not null)
            .SelectMany(static control => Names(control)
                .Select(name => Find(control, name))
                .OfType<AvaloniaProperty>()
                .Where(static property => Parses(property.PropertyType))
                .Select(property => (control, property)));

    /// <summary>The travelling properties of the keyed list items under a control, each with its number inside its own item.</summary>
    private static List<(Control Control, AvaloniaProperty Property, int Number)> Keyed(Control root)
    {
        var numbers = new Dictionary<Control, int>();
        var states = new List<(Control, AvaloniaProperty, int)>();
        foreach (var (control, property) in All(root))
        {
            if (Html.Variants.BrowserModals.Property(control) != property && Html.Templating.ItemIdentity.ItemOf(control) is { } item)
            {
                var number = numbers.GetValueOrDefault(item);
                numbers[item] = number + 1;
                states.Add((control, property, number));
            }
        }

        return states;
    }

    /// <summary>Writes the travelling properties of a keyed list item in the item, its key in their names, so a plan repeats them with the item.</summary>
    public static void WriteItem(HtmlContext context, Control container)
    {
        foreach (var (control, property, number) in Keyed(container).Where(state => ReferenceEquals(Html.Templating.ItemIdentity.ItemOf(state.Control), container)))
        {
            context.Plan.Literal($"<input type=\"hidden\" name=\"{Prefix}{number.ToString(CultureInfo.InvariantCulture)}");
            context.Plan.Add(ItemKeySuffixNode.Instance);
            context.Plan.Literal("\" value=\"");
            Value(context, control, property, number);
            context.Plan.Literal("\">");
        }
    }

    /// <summary>
    /// A travelling property by the name a library gives it: one of the control's own, or an attached property registered
    /// for the control's type named <c>Owner.Name</c> (a library's state on a control it does not own).
    /// </summary>
    private static AvaloniaProperty? Find(Control control, string name) =>
        name.IndexOf('.', StringComparison.Ordinal) is var dot and > 0
            ? AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(control.GetType()).FirstOrDefault(property =>
                property.Name.AsSpan().SequenceEqual(name.AsSpan(dot + 1)) && property.OwnerType.Name.AsSpan().SequenceEqual(name.AsSpan(0, dot)))
            : AvaloniaPropertyRegistry.Instance.FindRegistered(control, name);

    /// <summary>The names of a control's travelling properties: those of <c>a:Web.State</c>, and the open property of an overlay owner marked <c>a:Web.Modal</c>.</summary>
    private static IEnumerable<string> Names(Control control) =>
        (Web.GetState(control) ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Concat(Html.Variants.BrowserModals.Property(control) is { } open ? [open.Name] : [])
            .Distinct(StringComparer.Ordinal);

    /// <summary>Writes each travelling property's value in its hidden input; an overlay's open state is written in its dialog, in its owner's data context.</summary>
    public static void Write(HtmlContext context, Control root)
    {
        var index = 0;
        foreach (var (control, property) in Of(root))
        {
            if (Html.Variants.BrowserModals.Property(control) == property)
            {
                index++;
                continue;
            }

            context.Plan.Literal($"<input type=\"hidden\" name=\"{Prefix}{index.ToString(CultureInfo.InvariantCulture)}\" value=\"");
            Value(context, control, property, index++);
            context.Plan.Literal("\">");
        }
    }

    /// <summary>
    /// Writes a property's value in its control's data context, with the state node a planned post restores it through
    /// when the property is bound two way to a model path: a page model behind a layout's outlet is a scope of its own,
    /// which the inputs, written after the page's tree, enter as the outlet does.
    /// </summary>
    private static void Value(HtmlContext context, Control control, AvaloniaProperty property, int index)
    {
        var plan = context.Plan;
        var model = control.DataContext;
        var slot = model is null || ReferenceEquals(model, plan.Scope) ? -1 : context.Models.IndexOf(model, ReferenceEqualityComparer.Instance);
        if (!plan.Planning || slot < 0)
        {
            Sealed(plan, plan.Scope, control, property, index);
            Record(plan, control, property, index);
            return;
        }

        plan.Begin(model);
        Sealed(plan, model, control, property, index);
        Record(plan, control, property, index);
        plan.Add(new ScopeNode(slot, plan.End()));
    }

    /// <summary>Writes a property's value as a state value node, which a web answer seals (decision "Sealed control state").</summary>
    private static void Sealed(Plans.PlanRecorder plan, object? scope, Control control, AvaloniaProperty property, int index)
    {
        plan.Begin(scope);
        plan.Value(control, property);
        plan.Add(new StateValueNode(Prefix + index.ToString(CultureInfo.InvariantCulture), plan.End()));
    }

    /// <summary>The state node of a travelling property bound two way to a model path, of a type a post's text parses to without Avalonia.</summary>
    public static void Record(Plans.PlanRecorder plan, AvaloniaObject control, AvaloniaProperty property, int index)
    {
        if (plan.PathOf(control, property) is { } path && Plans.LocalBindings.Of(control, property) is { } binding && Plans.BindingModes.WritesToSource(binding) && TypeName(property.PropertyType) is { } type)
        {
            plan.Add(new StateNode(index, new SlotSpec("state", path, type)));
        }
    }

    private static string? TypeName(Type type) =>
        type == typeof(string) ? "string" : type == typeof(int) ? "int" : type == typeof(double) ? "double" : type == typeof(bool) ? "bool" : type == typeof(bool?) ? "bool?" : null;

    /// <summary>A keyed item's travelling property's input name: its number in the item and the item's key.</summary>
    private static string Name(Control control, int number) =>
        Prefix + number.ToString(CultureInfo.InvariantCulture) + ItemKeys.Suffix(Html.Templating.ItemIdentity.Around(control));

    /// <summary>Whether the post names a travelling state of a list item that does not stand where the page wrote it.</summary>
    public static bool Stale(Control root, IReadOnlyDictionary<string, string> form)
    {
        var posted = form.Keys.Where(static name => name.StartsWith(Prefix, StringComparison.Ordinal) && PlanForms.IsState(name) && ItemKeys.Keyed(name)).ToList();
        if (posted.Count == 0)
        {
            return false;
        }

        var written = Keyed(root).Select(static state => Name(state.Control, state.Number)).ToHashSet(StringComparer.Ordinal);
        return !posted.TrueForAll(written.Contains);
    }

    /// <summary>
    /// Restores the travelling properties a post carries and, when any was, settles the page again as it is mounted, so
    /// what the restored state shows (a list's other page) is realized before the post's fields are numbered.
    /// </summary>
    public static Action Restore(Window window, Control root, IReadOnlyDictionary<string, string> form)
    {
        var undo = new List<Action>();
        if (Restore(root, form, undo))
        {
            Hosting.PageWindows.Settle(window);
            window.UpdateLayout();
        }

        return () =>
        {
            undo.ForEach(static put => put());
            Hosting.PageWindows.Settle(window);
        };
    }

    /// <summary>Restores the travelling properties a post carries, noting what puts each back; whether any was restored.</summary>
    private static bool Restore(Control root, IReadOnlyDictionary<string, string> form, List<Action> undo)
    {
        var restored = false;
        var index = 0;
        foreach (var (control, property) in Of(root))
        {
            var prefix = Html.Variants.BrowserModals.Property(control) == property ? ModalPrefix : Prefix;
            restored |= Apply(control, property, form.GetValueOrDefault(prefix + index++.ToString(CultureInfo.InvariantCulture)), undo);
        }

        foreach (var (control, property, number) in Keyed(root))
        {
            restored |= Apply(control, property, form.GetValueOrDefault(Name(control, number)), undo);
        }

        return restored;
    }

    private static bool Apply(Control control, AvaloniaProperty property, string? text, List<Action> undo)
    {
        if (text is null || Parse(property.PropertyType, text) is not { } value)
        {
            return false;
        }

        var kept = control.GetValue(property);
        undo.Add(() => control.SetCurrentValue(property, kept));
        control.SetCurrentValue(property, value.Value);
        return true;
    }

    private static bool Parses(Type type) =>
        type == typeof(int) || type == typeof(double) || type == typeof(bool) || type == typeof(bool?) || type == typeof(string) || type.IsEnum;

    /// <summary>The value a hidden input's text stands for, or null when it does not parse.</summary>
    private static (object? Value, bool Parsed)? Parse(Type type, string text)
    {
        if (type == typeof(string))
        {
            return (text, true);
        }

        if (type == typeof(int))
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? (number, true) : null;
        }

        if (type == typeof(double))
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? (real, true) : null;
        }

        if (type == typeof(bool?) && text.Length == 0)
        {
            return (null, true);
        }

        if (type == typeof(bool) || type == typeof(bool?))
        {
            return bool.TryParse(text, out var flag) ? (flag, true) : null;
        }

        return Enum.TryParse(type, text, ignoreCase: false, out var named) && Enum.IsDefined(type, named) ? (named, true) : null;
    }
}
