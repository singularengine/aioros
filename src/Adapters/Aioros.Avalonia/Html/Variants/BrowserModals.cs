using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Markup;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// A library's overlays opened and closed in the browser (<c>a:Web.Modal</c>, Aioros.Cosmo's dialogs and sheets, as
/// Radix's <c>Dialog</c>): each owner's overlay is written before the page, once, in a <c>&lt;dialog&gt;</c> placed on
/// the panel the library covers, written while closed from a trace of the owner opened (its fields inert then: a post
/// neither binds nor presses them); <c>open</c> follows the owner's property, read from the model when it is bound. The
/// overlay's controls (<c>a:Web.ModalOf</c>) are left out where they sit and out of the post's prompts. Buttons marked
/// <c>a:Web.Opens</c> and <c>a:Web.Closes</c> open and close it with HTML's invoker commands; its state travels in the
/// page's control state (<c>_s{n}</c>, restored before a post binds), and a property bound two way is also posted, through
/// a hidden check box, as a change.
/// </summary>
internal sealed class BrowserModals(HtmlContext context)
{
    private const string FieldsetCss = "fieldset.mf{display:contents;border:0;margin:0;padding:0;min-width:0}\n";
    private readonly Dictionary<Control, string> _ids = [];
    private readonly Dictionary<Control, string> _anchors = [];
    private List<(Control Control, AvaloniaProperty Property)> _states = [];

    /// <summary>The id of the dialog being written, which a button marked <c>a:Web.Closes</c> closes.</summary>
    public string? Current { get; private set; }

    /// <summary>The overlay owner's own <c>bool</c> property that opens its overlay, when it is marked <c>a:Web.Modal</c>.</summary>
    public static AvaloniaProperty? Property(Control control) =>
        Web.GetModal(control) is { Length: > 0 } name && AvaloniaPropertyRegistry.Instance.FindRegistered(control, name) is { } property && property.PropertyType == typeof(bool)
            ? property
            : null;

    /// <summary>Whether a control is one a marked owner's library added over the page for its overlay.</summary>
    public static bool Owned(Control control) => Web.GetModalOf(control) is { } owner && Property(owner) is not null;

    /// <summary>The id of a marked owner's dialog.</summary>
    public string? Id(Control owner) => _ids.GetValueOrDefault(owner);

    /// <summary>The anchor name of a panel that a marked owner's overlay covers.</summary>
    public string? Anchor(Control host) => _anchors.GetValueOrDefault(host);

    /// <summary>Gives a marked owner the value a post carries for its check box: open while it is posted.</summary>
    public static void Bind(Control owner, string? value)
    {
        if (Property(owner) is { } property && BrowserFlags.Posted(owner, property))
        {
            owner.SetCurrentValue(property, value is not null);
        }
    }

    /// <summary>
    /// Numbers the page's marked owners and finds the panel each one's overlay covers (tracing a closed owner open for
    /// it), so the panel is written with the anchor its dialog is placed on.
    /// </summary>
    public void Prepare(Window window, Control root)
    {
        var owners = root.GetLogicalDescendants().Prepend(root).OfType<Control>().Where(control => Property(control) is not null).ToList();
        if (owners.Count == 0)
        {
            return;
        }

        context.Sheet.AddRule("prompt", PromptWriters.PromptCss);
        context.Sheet.AddRule("mf", FieldsetCss);
        _states = Forms.ControlStates.Of(root);
        for (var index = 0; index < owners.Count; index++)
        {
            var id = "dlg" + (index + 1).ToString(CultureInfo.InvariantCulture);
            _ids[owners[index]] = id;
            Shown(window, owners[index], Property(owners[index])!, controls =>
            {
                if (controls.FirstOrDefault()?.GetVisualParent() is Panel host)
                {
                    _anchors.TryAdd(host, "dh" + id[3..]);
                }
            });
        }
    }

    /// <summary>Writes a marked owner's dialog where the owner stands, in its data context; false for any other control.</summary>
    public bool Write(Control owner, Action<Control, ParentKind> write)
    {
        if (!_ids.ContainsKey(owner) || Property(owner) is not { } property || TopLevel.GetTopLevel(owner) is not Window window)
        {
            return false;
        }

        var open = owner.GetValue(property) is true;
        var state = _states.FindIndex(entry => ReferenceEquals(entry.Control, owner) && entry.Property == property);
        Shown(window, owner, property, (controls, traced) => WriteDialog(owner, property, state, open, traced, controls, write), Enter);
        return true;
    }

    private static void Shown(Window window, Control owner, AvaloniaProperty property, Action<List<Control>> use) =>
        Shown(window, owner, property, (controls, _) => use(controls));

    /// <summary>
    /// Hands over an owner's overlay as the library shows it: the live one while it is open, or the one the library shows
    /// while Aioros sets <c>a:Web.Shows</c> on it (its property, and the model bound to it, untouched) while it is closed or
    /// open but not shown (a library that opened before it was attached), its property set above its binding for a library
    /// that ignores the hint; whether it was traced.
    /// </summary>
    private static void Shown(Window window, Control owner, AvaloniaProperty property, Action<List<Control>, bool> use, Action<List<Control>>? entering = null)
    {
        var controls = Overlay(window, owner);
        var probes = new List<IDisposable>();
        var shown = controls.Count == 0;
        if (shown)
        {
            Settled.Change(owner, () => Web.SetShows(owner, true));
            Hosting.Realization.LayOut(window);
            controls = Overlay(window, owner);
        }

        if (controls.Count == 0)
        {
            if (owner.GetValue(property) is true)
            {
                Settled.Change(owner, () => probes.Add(LocalBindings.Probe(owner, property, false)));
            }

            Settled.Change(owner, () => probes.Add(LocalBindings.Probe(owner, property, true)));
            Hosting.Realization.LayOut(window);
            controls = Overlay(window, owner);
        }

        try
        {
            entering?.Invoke(controls);
            controls.ForEach(Settled.Rebuilt);
            use(controls, shown);
        }
        finally
        {
            probes.Reverse();
            probes.ForEach(probe => Settled.Change(owner, probe.Dispose));
            if (shown)
            {
                Settled.Change(owner, () => owner.ClearValue(Web.ShowsProperty));
            }
        }
    }

    /// <summary>Reads the entrances the overlay's parts started as the library showed them, before they are settled for writing.</summary>
    private void Enter(List<Control> controls)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        controls.ForEach(control => context.Entrances = context.Entrances.SetItems(Entrances.Settle(control)));
    }

    /// <summary>The controls a library shows over the page for an owner, outermost first.</summary>
    private static List<Control> Overlay(Window window, Control owner) =>
        [.. window.GetVisualDescendants().OfType<Control>()
            .Where(control => ReferenceEquals(Web.GetModalOf(control), owner) && !control.GetVisualAncestors().OfType<Control>().Any(outer => ReferenceEquals(Web.GetModalOf(outer), owner)))];

    private void WriteDialog(Control owner, AvaloniaProperty property, int state, bool open, bool traced, List<Control> controls, Action<Control, ParentKind> write)
    {
        var plan = context.Plan;
        var id = _ids[owner];
        var start = context.Fields.Count;
        var host = controls.FirstOrDefault()?.GetVisualParent() as Control;
        var size = host?.Bounds.Size ?? default;
        var surfaces = controls.Where(control => PromptWriters.Mask(control, size) is null).ToList();
        var labels = PromptWriters.Labels(context, surfaces, id);
        var posted = BrowserFlags.Posted(owner, property);
        var bound = LocalBindings.Of(owner, property) is not null;
        plan.Literal($"<dialog id=\"{id}\" class=\"prompt\" data-aioros-modal=\"{Forms.ControlStates.ModalPrefix}{state.ToString(CultureInfo.InvariantCulture)}\"");
        plan.Literal(Web.GetLightDismiss(owner) ? " data-aioros-dismiss" : string.Empty);
        plan.Literal(Web.GetModalKey(owner) is { } key ? Writers.HotKeys.Attributes(key) : string.Empty);
        plan.Literal(posted ? " data-change" : string.Empty);
        plan.Literal(bound || context.Answering ? " data-aioros-known" : string.Empty);
        WhileOpen(owner, property, open, " open", null);
        plan.Literal(host is not null && Anchor(host) is { } anchor ? $"{labels} style=\"position-anchor:--{anchor}\">" : $"{labels}>");
        plan.Literal($"<input type=\"hidden\" name=\"{Forms.ControlStates.ModalPrefix}{state.ToString(CultureInfo.InvariantCulture)}\" value=\"");
        WhileOpen(owner, property, open, "True", "False");
        plan.Literal("\">");
        Forms.ControlStates.Record(plan, owner, property, state);
        if (posted)
        {
            plan.Literal("<input type=\"checkbox\" hidden name=\"f");
            context.Field(owner);
            plan.Literal("\"");
            WhileOpen(owner, property, open, " checked", null);
            plan.Literal(">");
        }

        plan.Literal("<fieldset class=\"mf\"");
        WhileOpen(owner, property, open, null, " disabled");
        plan.Literal(">");
        var leaving = Leaving(owner, id, controls);
        var (current, inert) = (Current, context.Fields.Inert);
        (Current, context.Fields.Inert) = (id, traced);
        try
        {
            foreach (var control in controls)
            {
                if (PromptWriters.Mask(control, size) is { } fill)
                {
                    plan.Literal($"<div class=\"{PromptWriters.MaskClasses(context, control, fill)}\"></div>");
                    continue;
                }

                plan.Literal($"<div class=\"{context.Sheet.Class("c", PromptWriters.Placement(control))}\">");
                write(control, ParentKind.Single);
                plan.Literal("</div>");
            }
        }
        finally
        {
            (Current, context.Fields.Inert) = (current, inert);
        }

        plan.Literal("</fieldset></dialog>");
        if (!traced && !plan.Planning)
        {
            context.Answers.AddRange(Enumerable.Range(start, context.Fields.Count - start).Where(index => !context.Fields.IsInert(index)));
        }

        if (leaving.Length > 0)
        {
            context.Sheet.AddRule("ml" + CssValues.Hash(leaving), leaving);
        }
    }

    /// <summary>
    /// How a dialog's parts enter and leave: each part that enters by its transitions (its mask's fade, its surface's) has
    /// its entrance, and, while the dialog is not <c>open</c>, the look its theme gives it while <c>:closing</c> with its
    /// exit transitions, the dialog keeping its display and top layer for the longest of them; nothing moves for a
    /// visitor who asks for less motion.
    /// </summary>
    private string Leaving(Control owner, string id, List<Control> controls)
    {
        var rules = new System.Text.StringBuilder();
        var longest = TimeSpan.Zero;
        foreach (var control in controls)
        {
            var moving = context.Entrances.ContainsKey(control) ? control : control.GetVisualChildren().OfType<Control>().FirstOrDefault(context.Entrances.ContainsKey);
            if (moving is null || Entrances.Leaving(control, moving) is not { } leaving)
            {
                continue;
            }

            var css = leaving.Css + (leaving.Transitions is { } transitions ? $"transition:{transitions};" : string.Empty);
            var name = "lv" + CssValues.Hash(css);
            context.AddStateClass(moving, name, owner);
            rules.Append(CultureInfo.InvariantCulture, $"#{id}:not([open]) .{name}{{{css}}}");
            longest = leaving.Longest > longest ? leaving.Longest : longest;
        }

        if (rules.Length == 0)
        {
            return string.Empty;
        }

        var hold = CssValues.Ms(longest);
        return rules.Append(CultureInfo.InvariantCulture, $"#{id}{{transition:display {hold} allow-discrete,overlay {hold} allow-discrete}}@media (prefers-reduced-motion:reduce){{#{id}{{transition:none}}}}\n").ToString();
    }

    /// <summary>Writes one text while the owner is open and another while it is closed, read from the model when its property is bound.</summary>
    private void WhileOpen(Control owner, AvaloniaProperty property, bool open, string? whenOpen, string? whenClosed)
    {
        var plan = context.Plan;
        if (plan.Planning && plan.BoundPath(owner, property) is { } path)
        {
            var fallback = (property.GetMetadata(owner.GetType()) as IStyledPropertyMetadata)?.DefaultValue is true;
            foreach (var (negate, text) in new[] { (false, whenOpen), (true, whenClosed) })
            {
                if (text is not null)
                {
                    var slot = Flags.Of(path, negate, fallback);
                    plan.Add(new ConditionNode(slot.Getter, [new LiteralNode(text)]) { Source = slot.Spec });
                }
            }

            return;
        }

        plan.Literal((open ? whenOpen : whenClosed) ?? string.Empty);
    }
}
