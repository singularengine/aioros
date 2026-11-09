using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A tab control as the WAI-ARIA tabs pattern: a <c>role="tablist"</c> of <c>role="tab"</c> buttons (inside the box of
/// the strip its theme draws around its items presenter, as shadcn's <c>TabsList</c> in Aioros.Cosmo), the selected
/// one holding the hidden field that names its index, then one <c>role="tabpanel"</c> per tab, all but the selected one
/// <c>hidden</c>. The framework's keyboard module selects a tab on a click or with the keys, without a request. The list
/// takes its items panel's spacing as its gap and the panels take the content host's margin, whatever panel the template
/// lays them in.
/// </summary>
internal static class TabWriters
{
    private const string SelectedLook = "[aria-selected=\"true\"]";

    /// <summary>The border around the tab control's items presenter that draws a strip, or null.</summary>
    public static Border? Strip(TabControl tabs) =>
        ItemLooks.Part<ItemsPresenter>(tabs, "PART_ItemsPresenter")?.GetVisualParent() is Border { } border
        && (border.Background is not null || border.BorderThickness != default)
            ? border
            : null;

    public static void Write(HtmlContext context, TabControl tabs, ParentKind parent)
    {
        using var standIn = ItemParts.StandIn(context, tabs);
        var containers = ItemParts.Containers(tabs);
        var strip = Strip(tabs);
        var style = context.Box(tabs, parent);
        BoxStyles.Font(style, tabs.FontSize, tabs.FontWeight, tabs.Foreground);
        var paddings = Paddings(tabs, strip);
        BoxStyles.Fill(style, tabs.Background, tabs.BorderBrush, tabs.BorderThickness, tabs.CornerRadius, paddings.Around);
        style.Append(tabs.TabStripPlacement switch
        {
            Dock.Left => "flex-direction:row;",
            Dock.Right => "flex-direction:row-reverse;",
            Dock.Bottom => "flex-direction:column-reverse;",
            _ => "flex-direction:column;",
        });
        var spacing = strip?.GetVisualParent() is StackPanel stack ? stack.Spacing : 0;
        style.Append(spacing > 0 ? $"gap:{CssValues.Px(spacing)};" : string.Empty);
        context.Sheet.AddRule("tph", "[role=tabpanel][hidden]{display:none!important}\n");
        context.Plan.Literal($"<div data-aioros-tabs class=\"{context.Classes(tabs, "c", style)}\">");
        context.Fields.Add(tabs);
        var (items, headers) = ItemParts.Bodies(context, tabs, containers, container =>
            ItemParts.Content(context, ItemParts.Presenter(container, "PART_ContentPresenter"), ParentKind.Row, ListWriters.Looked(container)));
        var (selected, selection) = ItemParts.Selection(context, tabs, items is not null);
        var choices = new TabChoices(items, headers, selected, selection);
        WriteList(context, tabs, strip, containers, choices, paddings.List);
        WritePanels(context, tabs, containers, choices, paddings.Panel);
        context.Plan.Literal("</div>");
    }

    private static void WriteList(HtmlContext context, TabControl tabs, Border? strip, IReadOnlyList<Control> containers, TabChoices choices, Thickness padding)
    {
        var vertical = tabs.TabStripPlacement is Dock.Left or Dock.Right;
        var box = strip is null ? new StringBuilder() : context.Box(strip, ParentKind.Column);
        if (strip is not null)
        {
            BoxStyles.Fill(box, strip.Background, strip.BorderBrush, strip.BorderThickness, strip.CornerRadius, strip.Padding);
        }

        box.Append(strip is null && padding != default ? $"padding:{BoxStyles.Edges(padding)};" : string.Empty);
        box.Append(vertical ? "flex-direction:column;" : strip is null ? "flex-direction:row;flex-wrap:wrap;" : "flex-direction:row;align-items:center;");
        var gap = ItemLooks.Part<ItemsPresenter>(tabs, "PART_ItemsPresenter")?.Panel is StackPanel { Spacing: > 0 } items ? items.Spacing : 0;
        box.Append(gap > 0 ? $"gap:{CssValues.Px(gap)};" : string.Empty);
        var orientation = vertical ? " aria-orientation=\"vertical\"" : string.Empty;
        context.Plan.Literal($"<div role=\"tablist\"{orientation}{ItemParts.PostsChange(tabs)} class=\"{context.Sheet.Class("c", box)}\">");
        var margin = strip is null ? ItemLooks.Part<ItemsPresenter>(tabs, "PART_ItemsPresenter")?.Margin ?? default : default;
        var each = containers.Select(container => Tab(context, container, margin)).ToImmutableArray();
        var markup = each.FirstOrDefault() ?? Tab(null, string.Empty);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new ChoiceNode(choices.Items?.Getter, choices.Selected, markup, choices.Headers)
        {
            ItemsSource = choices.Items?.Spec, Selection = choices.Selection, Field = Forms.FieldSpecs.Of(context.Plan, tabs), Each = choices.Items is null ? each : [],
        }, tabs, containers));
        context.Plan.Literal("</div>");
    }

    private static void WritePanels(HtmlContext context, TabControl tabs, IReadOnlyList<Control> containers, TabChoices choices, Thickness padding)
    {
        var panel = new StringBuilder();
        var host = ItemLooks.Part<ContentPresenter>(tabs, "PART_SelectedContentHost")?.Margin ?? default;
        var margin = new Thickness(padding.Left + host.Left, padding.Top + host.Top, padding.Right + host.Right, padding.Bottom + host.Bottom);
        panel.Append(margin != default ? $"margin:{BoxStyles.Edges(margin)};" : string.Empty);
        panel.Append(tabs.TabStripPlacement is Dock.Left or Dock.Right ? "flex:1 1 auto;min-width:0;" : string.Empty);
        var name = context.Sheet.Class("c", panel);
        if (HidesEmpty(tabs))
        {
            var empty = "te" + CssValues.Hash(name);
            context.Sheet.AddRule(empty, $".{empty}:empty{{display:none}}\n");
            name = CssValues.Join(name, empty);
        }

        var (_, panels) = ItemParts.Bodies(context, tabs, containers, container => ListWriters.WriteTabContent(context, tabs, (TabItem)container));
        var markup = new ChoiceMarkup($"<div role=\"tabpanel\" hidden class=\"{name}\">", $"<div role=\"tabpanel\" class=\"{name}\">", null, "</div>");
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new ChoiceNode(choices.Items?.Getter, choices.Selected, markup, panels) { ItemsSource = choices.Items?.Spec, Selection = choices.Selection }, tabs, containers));
    }

    /// <summary>
    /// Whether the template's content host hides itself while the selected content is null (Aioros.Cosmo's tabs, whose
    /// strip alone is shadcn/ui's <c>TabsList</c>), read from the host or from its binding's converter given nothing: then a
    /// panel that holds nothing is out of the layout, as Avalonia's stack panel leaves out the hidden host and its spacing.
    /// </summary>
    private static bool HidesEmpty(TabControl tabs)
    {
        if (ItemLooks.Part<ContentPresenter>(tabs, "PART_SelectedContentHost") is not { } host)
        {
            return false;
        }

        if (tabs.SelectedContent is null)
        {
            return !host.IsVisible;
        }

        return global::Avalonia.Data.BindingOperations.GetBindingExpressionBase(host, Visual.IsVisibleProperty) is { } expression
            && TemplateBindings.Property(expression, tabs) == TabControl.SelectedContentProperty
            && TemplateConverters.Converter(expression) is { } converter
            && converter.Convert(null, typeof(bool), TemplateConverters.Parameter(expression), CultureInfo.InvariantCulture) is false;
    }

    private static ChoiceMarkup Tab(HtmlContext context, Control container, Thickness margin)
    {
        if (container.IsVisible || context.Flags.ShownInOther(container))
        {
            context.Flags.Visit(container);
        }
        else
        {
            context.Flags.Skip(container);
        }

        return Tab(container.IsEffectivelyEnabled ? null : " disabled", ListWriters.TabClass(context, container, margin, SelectedLook));
    }

    private static ChoiceMarkup Tab(string? disabled, string name) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"<button type=\"button\" role=\"tab\" aria-selected=\"false\" tabindex=\"-1\"{disabled} class=\"{name}\">"),
            string.Create(CultureInfo.InvariantCulture, $"<button type=\"button\" role=\"tab\" aria-selected=\"true\" tabindex=\"0\"{disabled} class=\"{name}\">"),
            "<input type=\"hidden\"",
            "</button>")
        { InputOnSelected = true };

    private readonly record struct TabChoices(Slot? Items, ImmutableArray<ImmutableArray<IPlanNode>> Headers, ChoicePredicate Selected, SlotSpec? Selection);

    /// <summary>
    /// Where the control's padding goes, as Avalonia applies it only where its template reads it: around the strip and
    /// the panel when the part reading it holds both (a root border's <c>Padding="{TemplateBinding Padding}"</c>, as
    /// Avalonia's Simple theme draws it), on the panel or on the list when it holds only one; never for the selected
    /// content's hosts (Fluent's and Semi.Avalonia's <c>PART_SelectedContentHost</c> and the transition's
    /// <c>PART_SelectedContentHost2</c> take it as their margin, written with the host's own) or for the strip's border,
    /// whose own padding is written.
    /// </summary>
    private static (Thickness Around, Thickness List, Thickness Panel) Paddings(TabControl tabs, Border? strip)
    {
        var items = ItemLooks.Part<ItemsPresenter>(tabs, "PART_ItemsPresenter");
        var host = ItemLooks.Part<ContentPresenter>(tabs, "PART_SelectedContentHost");
        var readers = tabs.GetVisualDescendants().OfType<Control>().Where(part => ReferenceEquals(part.TemplatedParent, tabs) && !ReferenceEquals(part, strip)
            && part.Name?.StartsWith("PART_SelectedContentHost", StringComparison.Ordinal) != true
            && AvaloniaPropertyRegistry.Instance.GetRegistered(part).Any(property => BindingOperations.GetBindingExpressionBase(part, property) is { } expression
                && TemplateBindings.Reads(expression, tabs, TemplatedControl.PaddingProperty))).ToList();
        var around = readers.Exists(part => Holds(part, items) && Holds(part, host));
        var list = readers.Exists(part => Holds(part, items) && !Holds(part, host));
        var panel = readers.Exists(part => Holds(part, host) && !Holds(part, items));
        return (around ? tabs.Padding : default, list ? tabs.Padding : default, panel ? tabs.Padding : default);

        static bool Holds(Control part, Control? inner) => inner is not null && part.IsVisualAncestorOf(inner);
    }
}
