using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Aioros.Runtime.Rendering;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// A library panel's own flag switched in the browser (<c>a:Web.Flag</c>, Aioros.Cosmo's <c>c:SidebarProvider.IsOpen</c>,
/// as shadcn/ui's sidebar switches its <c>data-state</c>): the panel is traced in the other value first, where only the
/// look of each control and whether it is written are kept, then written in its own value, each control whose look
/// differs carrying a class whose rules give it either look under the panel's <c>data-aioros-on</c> mark, and each
/// control shown in one value only written once, hidden in the other; a control the trace never met (one built again as
/// the flag changed) keeps its own look. Nothing is written twice, so the form's fields
/// keep their numbers, and the classes do not depend on the value the panel is written in: a flag bound to the model only
/// decides the mark, the one part of the panel that follows the model. A panel with <c>a:Web.Breakpoint</c> is traced a
/// third time in a window narrower than it, with its sheet open (<c>a:Web.Sheet</c>), and the looks that differ there
/// are given under the breakpoint's media query, as shadcn/ui's sidebar turns into a sheet under <c>useIsMobile</c>.
/// Flagged panels nest (Aioros.Cosmo's collapsible menu items inside its sidebar, as shadcn/ui's Radix
/// <c>Collapsible</c>s inside its provider): each traces its values in every value of the panels around it, and a
/// control's rules name the marks of every panel around it, from the outermost in.
/// </summary>
internal sealed partial class BrowserFlags(HtmlContext context)
{
    private const char Current = 'c';
    private const char Other = 'o';
    private const char Narrowed = 'n';
    private const string None = "display:none;";
    private const string Placeholder = "\u0001";
    private readonly List<Frame> _frames = [];
    private readonly Dictionary<Control, Dictionary<string, List<string>>> _looks = [];
    private readonly Dictionary<Control, HashSet<string>> _skipped = [];
    private readonly Dictionary<Control, HashSet<string>> _hidden = [];
    private readonly Dictionary<Control, int> _calls = [];
    private readonly Dictionary<Control, Dictionary<string, object>> _tips = [];

    private readonly Dictionary<Control, Dictionary<string, bool>> _expanded = [];

    /// <summary>The combination being written: each flag's value relative to its own, each width query's as whether it holds, since a nested container's frame is made again in each trace around it.</summary>
    private string Key => string.Concat(_frames.Select(frame => frame.Query is null || frame.State == Narrowed ? frame.State : (frame.State == Current) == frame.On ? Current : Other));

    private bool Tracing => _frames.Exists(frame => frame.State != Current);

    private bool Final => _frames.Count > 0 && !Tracing;

    /// <summary>Whether a container query or a bound class is being expanded, writing each control once per case.</summary>
    public bool Framed => _frames.Count > 0;

    /// <summary>The panel's flag the browser switches, when it names a <c>bool</c> property of the library's own.</summary>
    public static AvaloniaProperty? Property(Control control) =>
        control is Panel panel && Web.GetFlag(panel) is { } name && AvaloniaPropertyRegistry.Instance.FindRegistered(panel, name) is { } property
        && property.PropertyType == typeof(bool) && LibraryValues.FromLibrary(property.OwnerType)
            ? property
            : null;

    /// <summary>The viewport width under which a flagged panel becomes narrow (<c>a:Web.Breakpoint</c>), when it sets one.</summary>
    public static double? Breakpoint(Control control) =>
        control is Panel panel && Web.GetBreakpoint(panel) is var width && width > 0 ? width : null;

    /// <summary>The panel's property a sheet inside it stands for (<c>a:Web.Sheet</c>, Aioros.Cosmo's <c>IsMobileOpen</c>), when it has one.</summary>
    private static AvaloniaProperty? SheetProperty(Control panel) =>
        panel.GetVisualDescendants().OfType<Control>().Select(Web.GetSheet).FirstOrDefault(name => name is not null) is { } name
        && AvaloniaPropertyRegistry.Instance.FindRegistered(panel, name) is { } property && property.PropertyType == typeof(bool)
            ? property
            : null;

    /// <summary>
    /// Writes the panel with both values of its flag, or a container in and out of each width query that names it
    /// (<see cref="ContainerQueries"/>); false when it has neither left to trace.
    /// </summary>
    public bool Write(Control control, Action write)
    {
        Frame frame;
        if (Property(control) is { } property && !_frames.Exists(frame => ReferenceEquals(frame.Panel, control) && frame.Flag is not null))
        {
            frame = new Frame(control, property, control.GetValue(property) is true, Breakpoint(control), null);
        }
        else if (ContainerQueries.Next(control, _frames.Where(frame => ReferenceEquals(frame.Panel, control)).Select(frame => frame.Query?.Style).OfType<ContainerQuery>()) is { } query)
        {
            if (!_frames.Exists(frame => frame.Query is not null) && TopLevel.GetTopLevel(control) is Window laid)
            {
                Hosting.Realization.LayOut(laid);
            }

            var width = _frames.Find(frame => frame.Query is not null && ReferenceEquals(frame.Panel, control))?.Width ?? ContainerQueries.Width(control);
            frame = new Frame(control, null, query.Holds(width), null, query) { Width = width };
        }
        else
        {
            return false;
        }

        if (frame.Narrows is not null && TopLevel.GetTopLevel(control) is Window shown)
        {
            Hosting.Realization.LayOut(shown);
        }

        _frames.Add(frame);
        try
        {
            if (frame.Query is null)
            {
                Trace(frame, Other, write);
            }
            else
            {
                TraceWidth(frame, write);
            }

            if (frame.Narrows is { } breakpoint)
            {
                TraceNarrow(frame, breakpoint, write);
            }

            write();
        }
        finally
        {
            _frames.RemoveAt(_frames.Count - 1);
            if (_frames.Count == 0)
            {
                _looks.Clear();
                _skipped.Clear();
                _hidden.Clear();
                _calls.Clear();
                _tips.Clear();
                _expanded.Clear();
            }
        }

        return true;
    }

    /// <summary>Writes the marks of the panel being written: its flag's name and, while it is on, <c>data-aioros-on</c>, read from the model when the flag is bound.</summary>
    public void WriteMarks(Control control)
    {
        if (Writing(control) is not { } frame)
        {
            return;
        }

        var plan = context.Plan;
        plan.Literal($" data-aioros-flag=\"{CssValues.Encode(frame.Flag!.Name)}\"");
        WriteOn(frame, " data-aioros-on");
        plan.Literal(control is Panel panel && Web.GetFlagCookie(panel) is { Length: > 0 } cookie ? $" data-aioros-cookie=\"{CssValues.Encode(cookie)}\"" : string.Empty);
        plan.Literal(frame.Narrows is { } breakpoint ? $" data-aioros-breakpoint=\"{CssValues.Num(breakpoint)}\"" : string.Empty);
        plan.Literal(Posted(control, frame.Flag) ? " data-change" : string.Empty);
    }

    /// <summary>
    /// Writes, first in the panel being written, the hidden check box that posts its flag when the flag is bound two way
    /// (the flag module checks it with the mark and posts it as a change), checked while the flag is on.
    /// </summary>
    public void WriteField(Control control)
    {
        if (Writing(control) is not { } frame || !Posted(control, frame.Flag!))
        {
            return;
        }

        context.Plan.Literal("<input type=\"checkbox\" hidden name=\"f");
        context.Field(control);
        context.Plan.Literal("\"");
        WriteOn(frame, " checked");
        context.Plan.Literal(">");
    }

    /// <summary>Gives a flagged panel the value a post carries for it: on while its check box is posted.</summary>
    public static void Bind(Control control, string? value)
    {
        if (Property(control) is { } property)
        {
            control.SetCurrentValue(property, value is not null);
        }
    }

    /// <summary>Whether the panel's flag is bound two way, so the model follows it when the browser switches it.</summary>
    public static bool Posted(Control control, AvaloniaProperty property) =>
        LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding);


    /// <summary>
    /// Opens, in the panel being written, the <c>&lt;dialog&gt;</c> a sheet is written in (<c>a:Web.Sheet</c>): laid out in
    /// its place above the panel's breakpoint, a modal covering the viewport below it, which the flag module opens; false
    /// for any other control.
    /// </summary>
    public bool OpenSheet(Control control)
    {
        if (!Final || Web.GetSheet(control) is null || _frames.LastOrDefault(frame => frame.Narrows is not null)?.Narrows is not { } breakpoint)
        {
            return false;
        }

        var name = "sh" + CssValues.Num(breakpoint).Replace('.', '_');
        context.Sheet.AddRule(name, string.Create(CultureInfo.InvariantCulture,
            $"@media (min-width:{CssValues.Num(breakpoint)}px){{dialog.{name}{{display:contents}}}}\n" +
            $"@media (max-width:{CssValues.Num(breakpoint - 0.02)}px){{dialog.{name}{{position:fixed;inset:0;width:100%;height:100%;max-width:none;max-height:none;margin:0;padding:0;border:0;" +
            $"background:transparent;color:inherit;overflow:visible}}dialog.{name}:not([open]){{display:none}}dialog.{name}::backdrop{{background:transparent}}dialog.{name}>*{{width:100%;height:100%}}}}\n"));
        context.Plan.Literal($"<dialog data-aioros-sheet class=\"{name}\">");
        return true;
    }

    /// <summary>Records a control a trace of another value writes.</summary>
    public void Visit(Control control)
    {
        if (!Tracing)
        {
            return;
        }

        var key = Key;
        Of(_looks, control).TryAdd(key, []);
        if (!key.Contains(Narrowed, StringComparison.Ordinal) && ToolTip.GetTip(control) is { } tip)
        {
            Of(_tips, control)[key] = tip;
        }
        if (control is Expander expander)
        {
            Of(_expanded, control)[key] = expander.IsExpanded;
        }
    }

    /// <summary>Records a control a trace of another value leaves out because it is hidden there.</summary>
    public void Skip(Control control)
    {
        if (Tracing)
        {
            Of(_skipped, control).Add(Key);
        }
    }

    /// <summary>Whether a control hidden in its panels' values is shown in another, so it is written hidden.</summary>
    public bool ShownInOther(Control control)
    {
        if (!Final || control.IsVisible || !_looks.ContainsKey(control))
        {
            return false;
        }

        Hide(control, Key);
        return true;
    }

    /// <summary>
    /// The classes of a control's look: in a panel being written, a class giving it its look in every combination of the
    /// values of the panels around it where they differ.
    /// </summary>
    public string Classes(Control control, string? classes, StringBuilder style)
    {
        if (_frames.Count == 0)
        {
            return context.Sheet.Class(classes, style);
        }

        var css = style.ToString();
        if (Tracing && _frames.Exists(frame => frame.Query is not null && frame.State == Other && ReferenceEquals(frame.Panel, control)))
        {
            return string.Empty;
        }

        if (Tracing)
        {
            if (_looks.TryGetValue(control, out var keyed) && keyed.TryGetValue(Key, out var looks))
            {
                looks.Add(css);
            }

            return context.Sheet.Class(classes, style);
        }

        var call = _calls.GetValueOrDefault(control);
        _calls[control] = call + 1;
        var (table, mine) = Table(control, css, call);
        if (table.Values.All(look => look is null || look == mine))
        {
            return context.Sheet.Class(classes, mine == css ? style : new StringBuilder(mine));
        }

        var template = Emit(0, string.Empty, [], table);
        var marker = "f" + CssValues.Hash(template);
        context.Sheet.AddRule(marker, template.Replace(Placeholder, marker, StringComparison.Ordinal));
        return CssValues.Join(classes, marker);
    }

    /// <summary>
    /// A control's look in every combination of the values of the panels around it, and the look it is written with: a
    /// combination where a control around it is hidden takes its own look, as does one the traces never met, but below a
    /// breakpoint, where it has none.
    /// </summary>
    private (Dictionary<string, string?> Table, string Mine) Table(Control control, string css, int call)
    {
        var hidden = control.GetVisualAncestors().OfType<Control>().Where(_hidden.ContainsKey).SelectMany(ancestor => _hidden[ancestor]).ToList();
        var current = Key;
        var table = new Dictionary<string, string?>();
        var ignored = new HashSet<string>();
        foreach (var key in Keys(0, string.Empty))
        {
            if (hidden.Exists(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            {
                ignored.Add(key);
            }
            else
            {
                var seen = Seen(control, key, current, call);
                table[key] = seen == current ? Mine(control, seen, css) : Traced(control, seen, call);
            }
        }

        var mine = ignored.Contains(current) ? table.Values.FirstOrDefault(look => look is not null) ?? css : table[current]!;
        foreach (var key in Keys(0, string.Empty).Where(key => ignored.Contains(key) || table[key] is null))
        {
            table[key] = key.Contains(Narrowed, StringComparison.Ordinal) ? null : mine;
        }

        return (table, mine);
    }

    /// <summary>
    /// The combination whose look a control takes in <paramref name="key"/>: a container's own queries never select it
    /// (Avalonia looks for a query's container among the control's ancestors, as CSS does), so its look is the one traced
    /// in either state of them, the state it is written in first.
    /// </summary>
    private string Seen(Control control, string key, string current, int call)
    {
        IEnumerable<string> seen = [key];
        for (var depth = 0; depth < _frames.Count; depth++)
        {
            if (_frames[depth].Query is not null && ReferenceEquals(_frames[depth].Panel, control))
            {
                var at = depth;
                seen = seen.SelectMany(variant => (string[])[Swap(variant, at, current[at]), Swap(variant, at, current[at] == Current ? Other : Current)]);
            }
        }

        return seen.FirstOrDefault(variant => variant == current || Recorded(control, variant, call) is not null || Skipped(control, variant)) ?? key;
    }

    private string? Traced(Control control, string key, int call) => Recorded(control, key, call) ?? (Skipped(control, key) ? Hide(control, key) : null);

    private string Mine(Control control, string key, string css) => _hidden.TryGetValue(control, out var keys) && keys.Contains(key) ? None : css;

    private Frame? Writing(Control control) => Final && _frames[^1].Flag is not null && ReferenceEquals(_frames[^1].Panel, control) ? _frames[^1] : null;

    private void WriteOn(Frame frame, string text)
    {
        if (On(frame) is { } slot)
        {
            context.Plan.Add(new ConditionNode(slot.Getter, [new LiteralNode(text)]) { Source = slot.Spec });
        }
        else if (frame.On)
        {
            context.Plan.Literal(text);
        }
    }

    private Slot? On(Frame frame) => context.Plan.Planning && context.Plan.BoundPath(frame.Panel, frame.Flag!) is not null ? context.Plan.BoundFlag(frame.Panel, frame.Flag!) : null;

    /// <summary>Every combination of the values of the panels from <paramref name="depth"/> in, after <paramref name="key"/>: each panel's own, its other and, with a breakpoint, its narrow one.</summary>
    private IEnumerable<string> Keys(int depth, string key) =>
        depth == _frames.Count
            ? [key]
            : States(_frames[depth]).SelectMany(state => Keys(depth + 1, key + state));

    private static IEnumerable<char> States(Frame frame) => frame.Narrows is null ? [Current, Other] : [Current, Other, Narrowed];

    private IEnumerable<string?> Leaves(int depth, string key, Dictionary<string, string?> table) => Keys(depth, key).Select(leaf => table[leaf]);

    /// <summary>
    /// The rules giving a control its looks, from the panel at <paramref name="depth"/> in: one look under any value of a
    /// panel where both give the same, each under its mark otherwise, and the narrow look under its breakpoint's media query.
    /// </summary>
    private string Emit(int depth, string key, List<string> chain, Dictionary<string, string?> table)
    {
        if (depth == _frames.Count)
        {
            return table[key] is { } css ? Rule(chain, css) : string.Empty;
        }

        var frame = _frames[depth];
        if (frame.Query is { } query)
        {
            var held = Emit(depth + 1, key + Current, chain, table);
            return Leaves(depth + 1, key + Current, table).SequenceEqual(Leaves(depth + 1, key + Other, table))
                ? held
                : $"{ContainerQueries.Prelude(frame.Panel, query, true)}{{{held}}}\n{ContainerQueries.Prelude(frame.Panel, query, false)}{{{Emit(depth + 1, key + Other, chain, table)}}}\n";
        }

        var name = frame.Flag!.Name;
        var (onKey, offKey) = frame.On ? (key + Current, key + Other) : (key + Other, key + Current);
        var panel = $":where([data-aioros-flag=\"{name}\"])";
        var wide = Leaves(depth + 1, onKey, table).SequenceEqual(Leaves(depth + 1, offKey, table))
            ? Emit(depth + 1, key + Current, [.. chain, panel], table)
            : Emit(depth + 1, onKey, [.. chain, $":where([data-aioros-flag=\"{name}\"][data-aioros-on])"], table)
              + Emit(depth + 1, offKey, [.. chain, $":where([data-aioros-flag=\"{name}\"]:not([data-aioros-on]))"], table);
        if (frame.Narrows is not { } breakpoint || Leaves(depth + 1, key + Narrowed, table).All(look => look is null))
        {
            return wide;
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"@media (min-width:{CssValues.Num(breakpoint)}px){{{wide}}}\n@media (max-width:{CssValues.Num(breakpoint - 0.02)}px){{{Emit(depth + 1, key + Narrowed, [.. chain, panel], table)}}}\n");
    }

    private static string Rule(List<string> chain, string css)
    {
        var self = $".{Placeholder}.{Placeholder}";
        return chain.Count == 0
            ? $"{self}{{{css}}}\n"
            : $"{string.Join(' ', chain)} {self},{string.Join(' ', chain[..^1].Append(self + chain[^1]))}{{{css}}}\n";
    }

    private string? Recorded(Control control, string key, int call) =>
        _looks.TryGetValue(control, out var keyed) && keyed.TryGetValue(key, out var looks) && call < looks.Count ? looks[call] : null;

    private bool Skipped(Control control, string key) => _skipped.TryGetValue(control, out var keys) && keys.Contains(key);

    private string Hide(Control control, string key)
    {
        Of(_hidden, control).Add(key);
        return None;
    }

    private static TValue Of<TValue>(Dictionary<Control, TValue> map, Control control)
        where TValue : class, new()
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(map, control, out _);
        return value ??= new();
    }

    private static string Swap(string key, int depth, char state) => string.Concat(key[..depth], state.ToString(), key[(depth + 1)..]);

    private void Trace(Frame frame, char state, Action write)
    {
        var fields = context.Fields.Mark();
        var popovers = context.Popovers;
        var reserved = context.Plan.Popovers();
        IDisposable? probe = null;
        frame.State = state;
        try
        {
            Settled.Change(frame.Panel, () => probe = LocalBindings.Probe(frame.Panel, frame.Flag!, !frame.On));
            context.Plan.Begin(context.Plan.Scope);
            write();
            context.Plan.End();
        }
        finally
        {
            frame.State = Current;
            Settled.Change(frame.Panel, () => probe?.Dispose());
            context.Fields.Rewind(fields);
            context.Popovers = popovers;
            context.Plan.ForgetPopovers(reserved);
        }
    }

    /// <summary>
    /// Traces a container in the other state of its query, given the width that state and those of the container's other
    /// queries traced around it ask for; a state no width gives is not traced, and its looks are the container's own.
    /// </summary>
    private void TraceWidth(Frame frame, Action write)
    {
        frame.State = Other;
        var states = _frames.Where(other => other.Query is not null && ReferenceEquals(other.Panel, frame.Panel))
            .Select(other => (other.Query!, other.State == Current ? other.On : !other.On)).ToList();
        frame.State = Current;
        if (ContainerQueries.Probe(frame.Panel, states) is not { } width)
        {
            return;
        }

        var fields = context.Fields.Mark();
        var popovers = context.Popovers;
        var reserved = context.Plan.Popovers();
        IDisposable? probe = null;
        frame.State = Other;
        try
        {
            Settled.Change(frame.Panel, () => probe = frame.Panel.SetValue(Layoutable.WidthProperty, width, BindingPriority.Animation));
            Settled.Rebuilt(frame.Panel);
            context.Plan.Begin(context.Plan.Scope);
            write();
            context.Plan.End();
        }
        finally
        {
            frame.State = Current;
            Settled.Change(frame.Panel, () => probe?.Dispose());
            Settled.Rebuilt(frame.Panel);
            context.Fields.Rewind(fields);
            context.Popovers = popovers;
            context.Plan.ForgetPopovers(reserved);
        }
    }

    /// <summary>
    /// Traces the panel in a window just narrower than its breakpoint with its flag on and its sheet open, recording each
    /// control's look there, then gives the window its width back; the library reads the shown window's width as
    /// Aioros.Cosmo's provider does, so a panel with a breakpoint is written in a shown window, as a post's answer is.
    /// </summary>
    private void TraceNarrow(Frame frame, double breakpoint, Action write)
    {
        if (TopLevel.GetTopLevel(frame.Panel) is not Window window)
        {
            return;
        }

        var control = frame.Panel;
        var width = window.Width;
        var sheet = SheetProperty(control);
        var fields = context.Fields.Mark();
        var popovers = context.Popovers;
        var reserved = context.Plan.Popovers();
        IDisposable? probe = null;
        IDisposable? on = null;
        frame.State = Narrowed;
        try
        {
            Settled.Change(control, () => window.Width = Math.Max(1, Math.Ceiling(breakpoint) - 1));
            Settled.Rebuilt(control);
            Settled.Change(control, () =>
            {
                on = LocalBindings.Probe(control, frame.Flag!, true);
                probe = sheet is null ? null : LocalBindings.Probe(control, sheet, true);
            });
            Settled.Rebuilt(control);
            context.Plan.Begin(context.Plan.Scope);
            write();
            context.Plan.End();
        }
        finally
        {
            frame.State = Current;
            Settled.Change(control, () =>
            {
                probe?.Dispose();
                on?.Dispose();
            });
            Settled.Change(control, () => window.Width = width);
            Settled.Rebuilt(control);
            context.Fields.Rewind(fields);
            context.Popovers = popovers;
            context.Plan.ForgetPopovers(reserved);
        }
    }

    /// <summary>
    /// A flagged panel being written, or a container and one of its width queries: its flag or query, the value it is
    /// written in (whether the query holds), its breakpoint, and the value its pass traces.
    /// </summary>
    private sealed class Frame(Control panel, AvaloniaProperty? property, bool on, double? breakpoint, ContainerQueries.Query? query)
    {
        public Control Panel => panel;

        public AvaloniaProperty? Flag => property;

        public ContainerQueries.Query? Query => query;

        public double Width { get; init; }

        public bool On => on;

        public double? Narrows => breakpoint;

        public char State { get; set; } = Current;
    }
}
