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
using Avalonia.Layout;

namespace Aioros.Avalonia.Html.Writers;

internal static class ListWriters
{
    public static void WriteListBox(HtmlContext context, ListBox list, ParentKind parent)
    {
        using var standIn = ItemParts.StandIn(context, list);
        var containers = ItemParts.Containers(list);
        var multiple = list.SelectionMode.HasFlag(SelectionMode.Multiple);
        var style = context.Box(list, parent);
        BoxStyles.Font(style, list.FontSize, list.FontWeight, list.Foreground);
        BoxStyles.Fill(style, list.Background, list.BorderBrush, list.BorderThickness, list.CornerRadius, list.Padding);
        style.Append(Flow(list.ItemsPanelRoot));
        style.Append(double.IsNaN(list.Height) && double.IsInfinity(list.MaxHeight) ? string.Empty : "overflow-y:auto;");
        context.Plan.Literal($"<div role=\"{(multiple ? "group" : "radiogroup")}\"{ItemParts.PostsChange(list)} class=\"{context.Classes(list, "c", style)}\">");
        var item = containers.Count > 0 ? ItemClass(context, list, containers[0]) : string.Empty;
        var input = ItemParts.Input(context, multiple);
        var markup = new ChoiceMarkup($"<label class=\"{item}\">", $"<label class=\"{item}\">", input, "</label>");
        var positions = PositionClasses(context, list, ref containers);
        WriteChoices(context, list, containers, markup, container =>
        {
            Named(context, container);
            ItemParts.Content(context, ItemParts.Presenter(container, "PART_ContentPresenter"), ParentKind.Column, looked: true);
        }, [.. positions.Select(name => new ChoiceMarkup($"<label class=\"{name}\">", $"<label class=\"{name}\">", input, "</label>"))]);
        context.Plan.Literal("</div>");
    }

    public static void WriteTabStrip(HtmlContext context, TabStrip strip, ParentKind parent)
    {
        var containers = ItemParts.Containers(strip);
        var style = context.Box(strip, parent);
        BoxStyles.Fill(style, strip.Background, strip.BorderBrush, strip.BorderThickness, strip.CornerRadius, strip.Padding);
        style.Append("flex-direction:row;flex-wrap:wrap;");
        context.Plan.Literal($"<div role=\"radiogroup\"{ItemParts.PostsChange(strip)} class=\"{context.Classes(strip, "c", style)}\">");
        var tab = containers.Count > 0 ? TabClass(context, containers[0], default) : string.Empty;
        var markup = new ChoiceMarkup($"<label class=\"{tab}\">", $"<label class=\"{tab}\">", ItemParts.Input(context, multiple: false), "</label>");
        WriteChoices(context, strip, containers, markup, container => ItemParts.Content(context, ItemParts.Presenter(container, "PART_ContentPresenter"), ParentKind.Row, Looked(container)));
        context.Plan.Literal("</div>");
    }

    public static void WriteTabControl(HtmlContext context, TabControl tabs, ParentKind parent) => TabWriters.Write(context, tabs, parent);

    public static string Flow(Panel? panel) => panel switch
    {
        StackPanel { Orientation: Orientation.Horizontal } stack => $"flex-direction:row;gap:{CssValues.Px(stack.Spacing)};",
        StackPanel stack when stack.Spacing > 0 => $"gap:{CssValues.Px(stack.Spacing)};",
        WrapPanel => "flex-direction:row;flex-wrap:wrap;",
        _ => string.Empty,
    };

    private static void WriteChoices(HtmlContext context, SelectingItemsControl list, IReadOnlyList<Control> containers, ChoiceMarkup markup, Action<Control> write, ImmutableArray<ChoiceMarkup> positions = default)
    {
        context.Fields.Add(list);
        var (items, bodies) = ItemParts.Bodies(context, list, containers, write);
        var (selected, selection) = ItemParts.Selection(context, list, items is not null);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new ChoiceNode(items?.Getter, selected, markup, bodies)
        {
            ItemsSource = items?.Spec, Selection = selection, Field = Forms.FieldSpecs.Of(context.Plan, list), Positions = positions.IsDefault ? [] : positions,
        }, list, containers));
    }

    /// <summary>
    /// The class of the item's label by position (first, middle, last, only) when a <c>:nth-child</c> style may give
    /// them different looks: from the list's containers, or from stand-in lists while a plan learns a bound list; empty
    /// when every position has the first item's class.
    /// </summary>
    private static ImmutableArray<string> PositionClasses(HtmlContext context, ListBox list, ref IReadOnlyList<Control> containers)
    {
        if (containers.Count == 0 || !Templating.ItemPositions.CountsChildren(list))
        {
            return [];
        }

        ImmutableArray<string> classes;
        if (context.Plan.Planning && LocalBindings.IsBound(list, ItemsControl.ItemsSourceProperty))
        {
            classes = Templating.ItemPositions.Containers(list, container => ItemClass(context, list, container));
            containers = ItemParts.Containers(list);
        }
        else
        {
            var all = containers;
            var at = (int index) => ItemClass(context, list, all[index]);
            classes = all.Count == 1 ? [at(0), at(0), at(0), at(0)] : [at(0), at(all.Count > 2 ? 1 : 0), at(all.Count - 1), at(0)];
        }

        return classes.IsEmpty || classes.Distinct(StringComparer.Ordinal).Count() == 1 ? [] : classes;
    }

    public static void WriteTabContent(HtmlContext context, TabControl tabs, TabItem item)
    {
        var host = ItemLooks.Part<ContentPresenter>(tabs, "PART_SelectedContentHost");
        if (item.IsSelected && host?.Child is { } selected)
        {
            context.Write(selected, ParentKind.Single);
            return;
        }

        if (host?.Parent is not Panel panel || item.Content is null)
        {
            return;
        }

        var hosted = new ContentPresenter { ContentTemplate = item.ContentTemplate ?? tabs.ContentTemplate, IsVisible = false };
        panel.Children.Add(hosted);
        hosted.Content = item.Content;
        ThemeLooks.ExpandTemplates(hosted);
        if (hosted.Child is { } child)
        {
            context.Write(child, ParentKind.Single);
        }

        hosted.Content = null;
        panel.Children.Remove(hosted);
    }

    public static bool Looked(Control container) => ItemLooks.Part<Border>(container, "PART_LayoutRoot") is not null;

    private static string ItemClass(HtmlContext context, ListBox list, Control container)
    {
        var style = new StringBuilder("position:relative;display:flex;flex-direction:column;cursor:pointer;");
        if (container is not ContentControl item)
        {
            return context.Sheet.Class(null, style);
        }

        var presenter = ItemParts.Presenter(container, "PART_ContentPresenter");
        style.Append(ItemBox(list, item, presenter));
        if (presenter is null)
        {
            return context.Sheet.Class(null, style);
        }

        style.Append(presenter.BorderThickness != default ? $"border-width:{BoxStyles.Edges(presenter.BorderThickness)};border-style:solid;" : string.Empty);
        style.Append(presenter.CornerRadius != default ? $"border-radius:{BoxStyles.Corners(presenter.CornerRadius)};" : string.Empty);
        style.Append(presenter.GetValue(TextBlock.LineHeightProperty) is var line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        return context.Sheet.Class(ItemLooks.Class(context, item, presenter, presenter, ItemLooks.Checked), style);
    }

    /// <summary>The item container's box from its theme: padding, sizes, the font its content presenter gives the content where it differs from the list's, and where its content sits.</summary>
    private static string ItemBox(ListBox list, ContentControl item, ContentPresenter? presenter)
    {
        var (weight, size) = presenter is null ? (item.FontWeight, item.FontSize) : (presenter.FontWeight, presenter.FontSize);
        var style = new StringBuilder();
        style.Append(item.Padding != default ? $"padding:{BoxStyles.Edges(item.Padding)};" : string.Empty);
        style.Append(item.MinHeight > 0 ? $"min-height:{CssValues.Px(item.MinHeight)};" : string.Empty);
        style.Append(!double.IsNaN(item.Height) ? $"height:{CssValues.Px(item.Height)};" : string.Empty);
        style.Append(weight != list.FontWeight ? $"font-weight:{(int)weight};" : string.Empty);
        style.Append(Math.Abs(size - list.FontSize) > 0.001 ? $"font-size:{CssValues.Px(size)};" : string.Empty);
        style.Append(item.VerticalContentAlignment switch
        {
            VerticalAlignment.Center => "justify-content:center;",
            VerticalAlignment.Bottom => "justify-content:flex-end;",
            _ => string.Empty,
        });
        style.Append(item.HorizontalContentAlignment switch
        {
            HorizontalAlignment.Center => "align-items:center;",
            HorizontalAlignment.Right => "align-items:flex-end;",
            _ => string.Empty,
        });
        return style.ToString();
    }

    /// <summary>An item's automation name as text only assistive technologies read, which names its input (an icon-only toggle group item).</summary>
    private static void Named(HtmlContext context, Control container)
    {
        if (global::Avalonia.Automation.AutomationProperties.GetName(container) is { Length: > 0 } name)
        {
            ToggleWriters.AddHidden(context);
            context.Plan.Literal($"<span class=\"vh\">{CssValues.Encode(name)}</span>");
        }
    }

    /// <summary>A tab's own size and minimum height, and the line height its header presenter sets.</summary>
    private static void Sizing(StringBuilder style, TemplatedControl item)
    {
        style.Append(item.MinHeight > 0 ? $"min-height:{CssValues.Px(item.MinHeight)};" : string.Empty);
        BoxStyles.Size(style, item);
        var line = ItemLooks.Part<ContentPresenter>(item, "PART_ContentPresenter")?.GetValue(TextBlock.LineHeightProperty) ?? double.NaN;
        style.Append(double.IsFinite(line) && line > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
    }

    public static string TabClass(HtmlContext context, Control container, Thickness strip, string selected = ItemLooks.Checked)
    {
        var button = selected != ItemLooks.Checked;
        var style = new StringBuilder("position:relative;display:flex;flex-direction:row;align-items:center;cursor:pointer;white-space:nowrap;");
        if (container is not TemplatedControl item || ItemLooks.Part<Border>(container, "PART_LayoutRoot") is not { } root)
        {
            return context.Classes(container, null, style);
        }

        BoxStyles.Font(style, item.FontSize, item.FontWeight, null);
        style.Append(item.Padding != default ? $"padding:{BoxStyles.Edges(item.Padding)};" : button ? "padding:0;" : string.Empty);
        Sizing(style, item);
        var margin = new Thickness(item.Margin.Left + strip.Left, item.Margin.Top + strip.Top, item.Margin.Right + strip.Right, item.Margin.Bottom + strip.Bottom);
        style.Append(margin != default ? $"margin:{BoxStyles.Edges(margin)};" : string.Empty);
        style.Append(root.CornerRadius != default ? $"border-radius:{CssValues.Px(root.CornerRadius.TopLeft)};" : string.Empty);
        style.Append(root.BorderThickness != default ? $"border-width:{BoxStyles.Edges(root.BorderThickness)};border-style:solid;" : button ? "border-width:0;" : string.Empty);
        style.Append(!item.IsEffectivelyEnabled && item.Opacity < 1 ? $"opacity:{CssValues.Num(item.Opacity)};" : string.Empty);
        var look = ItemLooks.Class(context, item, root, root, selected);
        var pipe = ItemLooks.Pipe(container);
        if (pipe.Length > 0)
        {
            var name = "tb" + CssValues.Hash(pipe);
            context.Sheet.AddRule(name, string.Create(CultureInfo.InvariantCulture, $".{name}::after{{content:\"\";position:absolute;display:none;{pipe}}}.{name}{selected}::after{{display:block}}\n"));
            look = CssValues.Join(look, name);
        }

        return context.Classes(container, look, style);
    }
}
