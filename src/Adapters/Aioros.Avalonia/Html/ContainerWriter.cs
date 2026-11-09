using Aioros.Markup;
using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Panels;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html;

/// <summary>
/// Writes a control no writer claims as a box: a panel's layout as CSS grid or flex, a link or a semantic tag where its
/// web hints ask for one, its children inside, each item of a plain items panel as a list item; or, for a wrapper that
/// adds nothing to its only child, the child alone.
/// </summary>
internal sealed class ContainerWriter(HtmlContext context)
{
    private PlanRecorder Plan => context.Plan;

    public void Write(Control control, ParentKind parent)
    {
        var style = context.Box(control, parent);
        style.Append(control.MinHeight > 0 && !LocalBindings.IsBound(control, Layoutable.MinHeightProperty) ? $"min-height:{CssValues.Px(control.MinHeight)};" : string.Empty);
        var (classes, kind) = PanelLayouts.Of(control, style, context.Sheet);
        PanelSurfaces.Surface(control, style);
        var isLink = LinkAddresses.Links(control);
        var tag = isLink ? "a" : SemanticTag(control);
        if (control is global::Avalonia.Controls.Primitives.TemplatedControl templated && StateStyles.StylesTemplate(templated))
        {
            StateStyles.RegisterTemplate(context, templated);
        }
        else
        {
            StateStyles.Register(context, control);
        }

        Plan.Literal($"<{tag}{AutomationSemantics.Attributes(context, control, tag)}");
        context.Attributes(control);
        Plan.Literal(Roving(control));
        RangeGroups.Attributes(context, control);
        DragWriters.Open(context, control, IsItemsPanel(control, out var owner) ? owner : null);
        if (isLink)
        {
            LinkAddresses.Write(Plan, control);
        }

        Plan.Literal($" class=\"{context.Classes(control, classes, style)}\"");
        context.DynamicStyle(control);
        Plan.Literal(IsItemsPanel(control, out _) && AutomationSemantics.Attributes(context, control, tag).Length == 0 ? " role=\"list\">" : ">");
        context.Flags.WriteField(control);
        WriteChildren(control, kind);
        RangeGroups.Close(context, control);
        ChoiceLists.Write(context, control);
        DragWriters.Close(context, control);
        Plan.Literal($"</{tag}>");
    }

    /// <summary>The only child of a wrapper that adds no box, no web hint, no bound style and no class of its owner's to it, written in its place; or nothing.</summary>
    public Control? PassThrough(Control control, ParentKind parent)
    {
        if (!PanelSurfaces.IsWrapper(control) || HasWebHints(control) || context.PartClasses.ContainsKey(control))
        {
            return null;
        }

        var children = control.GetVisualChildren().OfType<Control>().Take(2).ToList();
        if (children.Count != 1)
        {
            return null;
        }

        var box = context.Box(control, parent).ToString();
        return box.Length == 0 || (parent == ParentKind.Single && box is BoxStyles.Fills or BoxStyles.Grows) ? children[0] : null;
    }

    private string SemanticTag(Control control) => WebTags.Container(control, context.Sheet) ?? AutomationSemantics.Landmark(control) ?? "div";

    private static bool IsItemsPanel(Control control, out ItemsControl? items)
    {
        items = control is Panel && control.GetVisualParent() is ItemsPresenter { TemplatedParent: ItemsControl owner } && owner.GetType() == typeof(ItemsControl) ? owner : null;
        return items is not null;
    }

    private void WriteItem(global::Avalonia.Visual item, ParentKind kind)
    {
        context.Sheet.AddRule("li", ".li{display:contents}\n");
        Plan.Literal("<div role=\"listitem\" class=\"li\">");
        context.Write(item, kind);
        Plan.Literal("</div>");
    }

    private void WriteChildren(Control control, ParentKind kind)
    {
        if (control is ScrollViewer scroll && ScrollFrames.Frame(scroll) is { } frame)
        {
            if (ScrollFrames.Content(frame) is { } content)
            {
                context.Write(content, kind);
            }

            return;
        }

        if (Plan.Planning && control is Panel panel && control.GetVisualParent() is ItemsPresenter { TemplatedParent: ItemsControl items } && ListPlans.Applies(Plan, items))
        {
            ListPlans.Write(Plan, items, panel, container => ItemBody(panel, container, kind), Listed);
            return;
        }

        var listed = IsItemsPanel(control, out _);
        var children = control.GetVisualChildren().ToList();
        var list = Plan.Planning ? null : ItemIdentity.OfPanel(Plan, control, children);
        if (list is not null)
        {
            WriteBoundItems(children, kind, listed, list);
            return;
        }

        foreach (var child in children)
        {
            WriteChild(child, kind, listed);
        }
    }

    private void WriteChild(global::Avalonia.Visual child, ParentKind kind, bool listed)
    {
        if (listed)
        {
            WriteItem(child, kind);
        }
        else
        {
            context.Write(child, kind);
        }
    }

    /// <summary>The engine's items of a bound list: each with its key, counted in the region's shape, and its travelling states.</summary>
    private void WriteBoundItems(List<global::Avalonia.Visual> children, ParentKind kind, bool listed, ListedPanel list)
    {
        var outer = context.Fields.ItemKey;
        var mark = context.Fields.EnterList();
        foreach (var child in children)
        {
            context.EnterListItem(outer, list.Key, (child as Control)?.DataContext);
            WriteChild(child, kind, listed);
            if (child is Control written)
            {
                Forms.ControlStates.WriteItem(context, written);
            }
        }

        context.EnterItem(outer);
        context.Fields.LeaveList(mark);
    }

    /// <summary>A container written as an item of the panel that holds it, whatever list that panel is in.</summary>
    private ImmutableArray<IPlanNode> Listed(Control container) =>
        container.GetVisualParent() is Panel panel ? ItemBody(panel, container, PanelLayouts.Of(panel, new StringBuilder(), context.Sheet).Kind) : ItemBody(new StackPanel(), container, ParentKind.Column);

    private ImmutableArray<IPlanNode> ItemBody(Panel panel, Control item, ParentKind kind)
    {
        Plan.Begin(item.DataContext);
        if (IsItemsPanel(panel, out _))
        {
            WriteItem(item, kind);
        }
        else
        {
            context.Write(item, kind);
        }

        Forms.ControlStates.WriteItem(context, item);
        return Plan.End();
    }

    /// <summary>
    /// Avalonia's keyboard XY navigation (<c>XYFocus.NavigationModes</c> with <c>Keyboard</c>) as the framework's roving
    /// focus, on the panel that turns it on: the arrows of the panel's orientation move the focus, stopping at the ends; every
    /// item stays a tab stop unless the panel takes one (<c>KeyboardNavigation.TabNavigation="Once"</c>).
    /// </summary>
    private static string Roving(Control control)
    {
        if (!XyKeys(control) || control.Parent is Control parent && XyKeys(parent))
        {
            return string.Empty;
        }

        var axis = control is StackPanel stack ? (stack.Orientation == Orientation.Horizontal ? "horizontal" : "vertical") : "both";
        return string.Concat(
            $" data-aioros-roving=\"{axis}\" data-aioros-roving-ends=\"off\"",
            KeyboardNavigation.GetTabNavigation(control) == KeyboardNavigationMode.Once ? string.Empty : " data-aioros-roving-tabs=\"keep\"");
    }

    private static bool XyKeys(Control control) => control.GetValue(XYFocus.NavigationModesProperty).HasFlag(XYFocusNavigationModes.Keyboard);

    private static bool HasWebHints(Control control) =>
        Roving(control).Length > 0 || Web.GetSlots(control) is not null || Web.GetSlotActive(control) is not null || Web.GetCurrent(control) is not null || LocalBindings.IsBound(control, Web.CurrentProperty) || Web.GetHref(control) is not null || LocalBindings.IsBound(control, Web.HrefProperty) || Web.GetId(control) is not null || Web.GetTag(control) is not null
        || Web.GetTransitionName(control) is not null || !double.IsNaN(Web.GetHideAbove(control)) || !double.IsNaN(Web.GetHideBelow(control))
        || LocalBindings.IsBound(control, Layoutable.HeightProperty) || LocalBindings.IsBound(control, Layoutable.WidthProperty) || BoundStyles.Any(control)
        || DragWriters.Marks(control) || AutomationSemantics.Any(control) || FilteredLists.Marks(control) || ChoiceLists.Marks(control) || RangeGroups.Marks(control);
}
