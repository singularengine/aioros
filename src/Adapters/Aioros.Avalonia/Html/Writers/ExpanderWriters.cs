using Aioros.Markup;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// An expander whose control theme is not Fluent's, written through its template as a <c>&lt;details&gt;</c>: its
/// <c>&lt;summary&gt;</c> takes the look and states of the template's header toggle (<c>ExpanderHeader</c>) and holds
/// the toggle's own template parts (the header, a trailing chevron), each part's look under <c>:checked</c> applied
/// while the details are open; a toggle whose template is a panel (Aioros.Cosmo's collapsible, a ghost toggle beside
/// the header) holds that panel filling it, each of its named parts taking its look under the toggle's states while the
/// summary is in them; a toggle's own height is its summary's. The content follows with its presenter's box, below the spacing of a stack panel holding the toggle.
/// </summary>
internal static class ExpanderWriters
{
    /// <summary>The header toggle of an expander whose theme is not Fluent's, when its template root is a named border or a panel; or null.</summary>
    public static ToggleButton? Header(Expander expander)
    {
        if (ThemeLooks.FromFluent(expander, typeof(Expander)) || ItemLooks.Part<ToggleButton>(expander, "ExpanderHeader") is not { } toggle)
        {
            return null;
        }

        ThemeLooks.ExpandTemplates(toggle);
        return toggle.GetVisualChildren().FirstOrDefault() is Border { Name.Length: > 0 } or Panel ? toggle : null;
    }

    /// <summary>The <c>name</c> attribute of an expander inside an exclusive panel or list (<see cref="Web.ExclusiveProperty"/>), or nothing.</summary>
    public static string Group(HtmlContext context, Expander expander)
    {
        if (expander.GetVisualAncestors().OfType<Control>().FirstOrDefault(Web.GetExclusive) is not { } group)
        {
            return string.Empty;
        }

        if (!context.Groups.TryGetValue(group, out var name))
        {
            context.Groups[group] = name = "x" + (context.Groups.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return $" name=\"{name}\"";
    }

    public static void WriteThemed(HtmlContext context, Expander expander, ToggleButton toggle, StringBuilder style)
    {
        var top = (Control)toggle.GetVisualChildren().First();
        var look = top is Border { Name: { } name } ? ThemeLooks.Of(toggle, name, ThemeLooks.PressStates) : Look.None;
        var summary = new StringBuilder();
        BoxStyles.Font(summary, toggle.FontSize, toggle.FontWeight, Equals(toggle.Foreground, expander.Foreground) ? null : toggle.Foreground);
        summary.Append(look.Css);
        summary.Append(top is Border ? "display:block;" : "display:flex;flex-direction:column;");
        summary.Append(double.IsNaN(toggle.Height) ? string.Empty : $"height:{CssValues.Px(toggle.Height)};");
        summary.Append(Fits(toggle.HorizontalAlignment));
        context.Plan.Literal($"<details class=\"{context.Classes(expander, "c", style)}\"");
        Open(context, expander);
        context.Plan.Literal($"{Marks(context, expander)}><summary class=\"{context.Sheet.Class(context.Sheet.States(look), summary)}\">");
        OpenLooks(context, expander, toggle);
        if (top is Border root)
        {
            if (root.Child is { } parts)
            {
                context.Write(parts, ParentKind.Row);
            }
        }
        else
        {
            PartLooks(context, expander, toggle, top);
            context.Write(top, ParentKind.Single);
        }

        context.Plan.Literal("</summary>");
        WriteField(context, expander);
        WriteContent(context, expander, toggle.GetVisualParent() is StackPanel { Spacing: > 0 } stack ? stack.Spacing : 0);
        context.Plan.Literal("</details>");
    }

    /// <summary>The look each named part of a toggle's panel takes under the toggle's states (a ghost button's fill), written for the summary in those states.</summary>
    private static void PartLooks(HtmlContext context, Expander expander, ToggleButton toggle, Control panel)
    {
        foreach (var part in panel.GetVisualDescendants().OfType<Control>().Where(part => part.Name is { Length: > 0 } && ReferenceEquals(part.TemplatedParent, toggle)))
        {
            var look = ThemeLooks.Of(toggle, part.Name!, ThemeLooks.PressStates);
            if (look.States.Count == 0)
            {
                continue;
            }

            var rules = string.Concat(look.States.Select(state => $":{state.Selector} .|{{{state.Rule.Replace(";", "!important;", StringComparison.Ordinal)}}}"));
            var name = "xp" + CssValues.Hash(rules);
            context.Sheet.AddRule(name, string.Concat(look.States.Select(state => $"details>summary:{state.Selector} .{name}{{{state.Rule.Replace(";", "!important;", StringComparison.Ordinal)}}}")) + "\n");
            context.AddStateClass(part, name, expander);
        }
    }

    public static void Open(HtmlContext context, Expander expander) => WhileExpanded(context, expander, " open");

    /// <summary>The mark of an expander whose <c>IsExpanded</c> is bound two way: the browser's toggle is posted as a change.</summary>
    /// <summary>An expander's group and how its state is kept: following a panel's flag in the browser, or posted when bound two way.</summary>
    public static string Marks(HtmlContext context, Expander expander) =>
        Group(context, expander) + (context.Flags.Follows(expander) is { Length: > 0 } follows ? follows : Change(expander));

    public static string Change(Expander expander) => Posted(expander) ? " data-change" : string.Empty;

    /// <summary>
    /// Writes, right after the summary of an expander whose <c>IsExpanded</c> is bound two way, the hidden check box that
    /// posts it, checked while it is expanded (read from the model by the plan); the framework's script checks it as the
    /// details toggle and posts it as a change, as a bound flag's (ENGINE-259).
    /// </summary>
    public static void WriteField(HtmlContext context, Expander expander)
    {
        if (!Posted(expander))
        {
            return;
        }

        context.Plan.Literal("<input type=\"checkbox\" hidden name=\"f");
        context.Field(expander);
        context.Plan.Literal("\"");
        WhileExpanded(context, expander, " checked");
        context.Plan.Literal(">");
    }

    /// <summary>Gives an expander whose <c>IsExpanded</c> is posted the value a post carries: expanded while its check box is posted.</summary>
    public static void Bind(Expander expander, string? value)
    {
        if (Posted(expander))
        {
            expander.SetCurrentValue(Expander.IsExpandedProperty, value is not null);
        }
    }

    private static bool Posted(Expander expander) =>
        Plans.LocalBindings.Of(expander, Expander.IsExpandedProperty) is { } binding && Plans.BindingModes.WritesToSource(binding);

    private static void WhileExpanded(HtmlContext context, Expander expander, string text)
    {
        if (context.Plan.Planning && context.Plan.BoundPath(expander, Expander.IsExpandedProperty) is { } open)
        {
            context.Plan.Add(new Runtime.Rendering.ConditionNode(Plans.PropertyPaths.Getter(open), [new Runtime.Rendering.LiteralNode(text)]) { Source = Runtime.Rendering.SlotSpec.Bound(open) });
        }
        else if (expander.IsExpanded)
        {
            context.Plan.Literal(text);
        }
    }

    /// <summary>
    /// A summary only as wide as its toggle when the toggle is aligned rather than stretched (Radix's <c>w-fit</c>
    /// trigger), placed at the toggle's side.
    /// </summary>
    private static string Fits(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Left => "width:fit-content;",
        HorizontalAlignment.Center => "width:fit-content;margin-inline:auto;",
        HorizontalAlignment.Right => "width:fit-content;margin-left:auto;",
        _ => string.Empty,
    };

    /// <summary>
    /// The content's box: the presenter's own margin (after the template's spacing above it), border, padding and line
    /// height, as the trace's open step draws its left line 7 px in.
    /// </summary>
    private static void WriteContent(HtmlContext context, Expander expander, double gap)
    {
        var presenter = expander.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(part => part.Name == "PART_ContentPresenter" && ReferenceEquals(part.TemplatedParent, expander));
        var margin = presenter?.Margin ?? default;
        var box = new StringBuilder();
        if (margin != default)
        {
            box.Append(CultureInfo.InvariantCulture, $"margin:{BoxStyles.Edges(new Thickness(margin.Left, margin.Top + gap, margin.Right, margin.Bottom))};");
        }
        else if (gap > 0)
        {
            box.Append(CultureInfo.InvariantCulture, $"margin-top:{CssValues.Px(gap)};");
        }

        if (presenter is not null)
        {
            BoxStyles.Fill(box, null, presenter.BorderBrush, presenter.BorderThickness, default, presenter.Padding, resetBorder: false);
            box.Append(presenter.GetValue(TextBlock.LineHeightProperty) is var line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        }

        context.Plan.Literal($"<div class=\"{context.Sheet.Class("c", box)}\">");
        if (presenter?.Child is { } shown)
        {
            context.Write(shown, ParentKind.Column);
        }
        else
        {
            context.Content(expander.Content);
        }

        context.Plan.Literal("</div>");
    }

    /// <summary>
    /// The look each of the toggle's parts takes under <c>:checked</c> (a chevron turned), written for the open details: a
    /// part's settled transform, so a transition moving it is read at its end.
    /// </summary>
    private static void OpenLooks(HtmlContext context, Expander expander, ToggleButton toggle)
    {
        var parts = toggle.GetVisualDescendants().OfType<Control>().ToList();
        var classes = (IPseudoClasses)toggle.Classes;
        var was = toggle.Classes.Contains(":checked");
        classes.Remove(":checked");
        var closed = parts.Select(Transform).ToList();
        classes.Add(":checked");
        var opened = parts.Select(Transform).ToList();
        classes.Set(":checked", was);
        for (var index = 0; index < parts.Count; index++)
        {
            if (closed[index] == opened[index])
            {
                continue;
            }

            var name = "xo" + CssValues.Hash(closed[index] + "|" + opened[index]);
            context.Sheet.AddRule(name, $"details:not([open])>summary .{name}{{transform:{closed[index]}}}details[open]>summary .{name}{{transform:{opened[index]}}}\n");
            context.AddStateClass(parts[index], name, expander);
        }
    }

    private static string Transform(Control part) =>
        part.GetBaseValue(Visual.RenderTransformProperty) is { HasValue: true, Value: { Value.IsIdentity: false } transform } ? CssValues.Transform(transform) : "none";
}
