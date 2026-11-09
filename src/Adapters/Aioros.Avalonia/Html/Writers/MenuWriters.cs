using Aioros.Markup;
using System.Collections;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Menus as WAI-ARIA menus (the APG's menu and menubar patterns, Radix's DropdownMenu): a popover <c>role="menu"</c>
/// with the look of the surface Avalonia shows it in, items <c>role="menuitem"</c>, <c>menuitemcheckbox</c> or
/// <c>menuitemradio</c> (<c>aria-checked</c>) out of the tab order with their theme's look, a sub-menu's item
/// <c>aria-haspopup="menu"</c> opening a nested menu placed as its template's popup, separators as <c>&lt;hr&gt;</c> and
/// items that cannot take the focus (a label) as plain rows. The overlay module of the framework's script moves the focus
/// through the items (arrows, Home, End, typeahead, sub-menus) and with the pointer (<c>data-aioros-overlay</c>).
/// </summary>
internal static class MenuWriters
{
    public static void WriteMenu(HtmlContext context, Menu menu, ParentKind parent)
    {
        context.Sheet.AddRule("mb", ".mb{display:flex;flex-direction:row;align-items:stretch;list-style:none;margin:0;padding:0}.mb>li{display:flex}\n");
        if (menu.Bounds.Width <= 0)
        {
            ThemeLooks.ExpandTemplates(menu);
            menu.Measure(global::Avalonia.Size.Infinity);
            menu.Arrange(new global::Avalonia.Rect(menu.DesiredSize));
        }

        var style = context.Box(menu, parent);
        BoxStyles.Fill(style, menu.Background, menu.BorderBrush, menu.BorderThickness, menu.CornerRadius, menu.Padding);
        style.Append(double.IsNaN(menu.Height) ? string.Empty : $"min-height:{CssValues.Px(menu.Height)};height:auto;");
        BarLayout(style, menu);
        context.Plan.Literal($"<menu role=\"menubar\" data-aioros-overlay class=\"{context.Classes(menu, "mb", style)}\">");
        foreach (var item in menu.Items)
        {
            context.Plan.Literal("<li role=\"none\">");
            WriteBarItem(context, item, menu);
            context.Plan.Literal("</li>");
        }

        context.Plan.Literal("</menu>");
    }

    /// <summary>
    /// What the bar's template draws around its items: its root border's shadow, the items presenter's vertical alignment
    /// (the items at their own height where it is not stretched) and the items panel's spacing.
    /// </summary>
    private static void BarLayout(StringBuilder style, Menu menu)
    {
        if (menu.GetVisualChildren().FirstOrDefault() is Border { BoxShadow.Count: > 0 } root)
        {
            style.Append("box-shadow:").Append(BoxStyles.Shadow(root.BoxShadow)).Append(';');
        }

        if (menu.Presenter is not { } presenter)
        {
            return;
        }

        style.Append(presenter.VerticalAlignment switch
        {
            global::Avalonia.Layout.VerticalAlignment.Top => "align-items:flex-start;",
            global::Avalonia.Layout.VerticalAlignment.Center => "align-items:center;",
            global::Avalonia.Layout.VerticalAlignment.Bottom => "align-items:flex-end;",
            _ => string.Empty,
        });
        style.Append(presenter.Panel is StackPanel { Spacing: > 0 } panel ? $"gap:{CssValues.Px(panel.Spacing)};" : string.Empty);
    }

    /// <summary>Writes a menu popover: its surface's look when Avalonia showed it (else Fluent's resources), its placement and its items.</summary>
    public static void WritePopover(HtmlContext context, string id, IEnumerable items, Control owner, string placement, Control? surface, string? extra = null)
    {
        var entrance = surface is null ? null : FlyoutWriters.Entered(context, surface);
        var look = surface is null ? PopoverLook(context, owner) : context.Sheet.Class(null, SurfaceLooks.Of(surface));
        context.Plan.Literal($"<div popover id=\"{id}\" role=\"menu\" data-aioros-overlay class=\"{CssValues.Join(CssValues.Join("menu " + look, extra), entrance)}\" style=\"position-anchor:--{id};{placement}\">");
        foreach (var item in items)
        {
            WriteItem(context, item, owner);
        }

        context.Plan.Literal("</div>");
    }

    public static void WriteItem(HtmlContext context, object? item, Control owner)
    {
        switch (item)
        {
            case Separator separator:
                WriteSeparator(context, separator);
                break;
            case MenuItem { Header: "-" } line:
                WriteSeparator(context, line);
                break;
            case MenuItem { IsVisible: false }:
                break;
            case MenuItem { ItemCount: > 0 } parent:
                WriteParent(context, parent, owner);
                break;
            case MenuItem leaf:
                WriteLeaf(context, leaf, Realized(leaf) ? MenuItemLooks.Of(context, leaf) : null, "mi");
                break;
        }
    }

    private static bool Realized(MenuItem item) => item.Bounds.Width > 0;

    private static void WriteSeparator(HtmlContext context, Control separator)
    {
        var look = separator.Bounds.Width > 0 ? MenuItemLooks.Separator(context, separator) : null;
        context.Plan.Literal(look is null ? "<hr>" : $"<hr class=\"{look}\">");
    }

    /// <summary>An item with sub-items: it opens its sub-menu, which Avalonia shows in its template's popup, placed as that popup.</summary>
    private static void WriteParent(HtmlContext context, MenuItem parent, Control owner)
    {
        var id = context.NextPopover();
        var look = Realized(parent) ? MenuItemLooks.Of(context, parent) : null;
        context.Plan.Literal($"<button type=\"button\" role=\"menuitem\" aria-haspopup=\"menu\" tabindex=\"-1\" class=\"{CssValues.Join("mi", look?.Classes)}\" popovertarget=\"{id}\" style=\"anchor-name:--{id}\"{Disabled(parent)}>");
        WriteInside(context, parent, look);
        context.Plan.Literal(look?.Chevron ?? InputWriters.IconSvg("chevron-right", 14, 2, "sub"));
        context.Plan.Literal("</button>");
        WriteSubMenu(context, id, parent, owner, Placements.Of(PlacementMode.RightEdgeAlignedTop, 0, 0));
    }

    private static void WriteBarItem(HtmlContext context, object? item, Menu menu)
    {
        if (item is not MenuItem menuItem)
        {
            WriteItem(context, item, menu);
            return;
        }

        var look = Realized(menuItem) ? MenuItemLooks.Of(context, menuItem) : null;
        var classes = look is null ? BarLook(context, menu) : "mbi " + look.Classes;
        if (menuItem.ItemCount == 0)
        {
            WriteLeaf(context, menuItem, look, look is null ? classes : "mbi");
            return;
        }

        var id = context.NextPopover();
        context.Plan.Literal($"<button type=\"button\" role=\"menuitem\" aria-haspopup=\"menu\" class=\"{classes}\" popovertarget=\"{id}\" style=\"anchor-name:--{id}\"{Disabled(menuItem)}>");
        WriteHeader(context, menuItem);
        context.Plan.Literal("</button>");
        WriteSubMenu(context, id, menuItem, menu, Placements.Of(PlacementMode.BottomEdgeAlignedLeft, 0, 0));
    }

    /// <summary>A menu bar item's look from Fluent's resources, for a bar Avalonia did not lay out.</summary>
    private static string BarLook(HtmlContext context, Menu menu)
    {
        var padding = ThemeResources.Css(menu, "MenuBarItemPadding", "6px 10px");
        var foreground = ThemeResources.Css(menu, "MenuFlyoutItemForeground", "inherit");
        var hover = ThemeResources.Css(menu, "MenuFlyoutItemBackgroundPointerOver", "rgba(0,0,0,.05)");
        var classes = "mbi mb" + CssValues.Hash(padding + foreground + hover);
        context.Sheet.AddRule("mbi", ".mbi{display:flex;align-items:center;gap:8px;border:0;background:none;font-size:14px}.mbi:disabled{opacity:.45;cursor:default}\n");
        context.Sheet.AddRule(classes[4..], $".{classes[4..]}{{padding:{padding};color:{foreground}}}.{classes[4..]}:hover:not(:disabled),.{classes[4..]}:focus-visible,.{classes[4..]}:has(+:popover-open){{background:{hover}}}\n");
        return classes;
    }

    /// <summary>
    /// The sub-menu of an item, placed as the item's template popup and with the look of what that popup shows: the
    /// popup's surface is lent to the page for the time it is written (a closed popup lays nothing out), so it is styled,
    /// realizes its items and enters as Avalonia shows it, and is given back.
    /// </summary>
    private static void WriteSubMenu(HtmlContext context, string id, MenuItem parent, Control owner, string fallback)
    {
        var popup = parent.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        if (popup is null || !Realized(parent))
        {
            WritePopover(context, id, parent.Items, owner, fallback, null);
            return;
        }

        var placement = Placements.Of(popup);
        if (popup.Child is not { } surface || surface.IsAttachedToVisualTree())
        {
            WriteSubMenu(context, id, parent, owner, placement, popup.Child);
            return;
        }

        popup.SetCurrentValue(Popup.ChildProperty, null);
        try
        {
            Probes.With(parent, surface, shown =>
            {
                if (shown is null)
                {
                    WriteSubMenu(context, id, parent, owner, placement, surface);
                }
                else
                {
                    WritePopover(context, id, parent.Items, owner, placement, shown);
                }
            });
        }
        finally
        {
            popup.SetCurrentValue(Popup.ChildProperty, surface);
        }
    }

    /// <summary>Writes a sub-menu whose popup's surface is not lent to the page: its items lent to a presenter realized for the time they are written.</summary>
    private static void WriteSubMenu(HtmlContext context, string id, MenuItem parent, Control owner, string placement, Control? surface)
    {
        if (parent.ItemsSource is not null || parent.Items.Any(item => item is not Control))
        {
            WritePopover(context, id, parent.Items, owner, placement, surface);
            return;
        }

        var items = parent.Items.OfType<Control>().ToList();
        parent.Items.Clear();
        var probe = new MenuFlyoutPresenter();
        items.ForEach(item => probe.Items.Add(item));
        try
        {
            Probes.With(parent, probe, _ => WritePopover(context, id, items, owner, placement, surface));
        }
        finally
        {
            probe.Items.Clear();
            items.ForEach(item => parent.Items.Add(item));
        }
    }

    /// <summary>
    /// An item that does something: its command posted (<c>_btn</c>), a link, or a toggle without a command posted as a
    /// click, which Avalonia's item turns into its checked state as it does on the desktop; an item that cannot take the
    /// focus (a label) is a plain row.
    /// </summary>
    private static void WriteLeaf(HtmlContext context, MenuItem item, MenuItemLook? look, string classes)
    {
        var href = Web.GetHref(item);
        var checks = Checks(context, item);
        var (on, off) = checks is not null && look is not null ? (MenuItemLooks.Checked(context, item, true), MenuItemLooks.Checked(context, item, false)) : (look, look);
        var names = on?.Classes == off?.Classes ? CssValues.Join(classes, look?.Classes) : null;
        var tab = classes == "mi" ? " tabindex=\"-1\"" : string.Empty;
        if (item.Command is not null || (item.ToggleType != MenuItemToggleType.None && item.IsEffectivelyEnabled))
        {
            context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
            context.Button(item);
            context.Plan.Literal("|");
            context.Plan.Value(item, MenuItem.CommandParameterProperty);
            context.Plan.Literal("\" ");
            WriteRole(context, item, checks);
            context.Plan.Literal(tab);
            WriteClass(context, classes, names, checks, on, off);
            context.Plan.Literal($"{Disabled(item)}>");
            WriteInside(context, item, on ?? look, checks);
            context.Plan.Literal("</button>");
        }
        else if (href is not null || LocalBindings.IsBound(item, Web.HrefProperty))
        {
            context.Plan.Literal("<a ");
            WriteRole(context, item, checks);
            context.Plan.Literal(tab);
            WriteClass(context, classes, names, checks, on, off);
            context.Plan.Literal(" href=\"");
            context.Plan.Value(item, Web.HrefProperty, ValueFormat.Href);
            context.Plan.Literal("\">");
            WriteInside(context, item, on ?? look, checks);
            context.Plan.Literal("</a>");
        }
        else if (item.Focusable)
        {
            var disabled = Disabled(item).Length > 0 ? " aria-disabled=\"true\"" : string.Empty;
            context.Plan.Literal("<div ");
            WriteRole(context, item, checks);
            context.Plan.Literal(tab);
            WriteClass(context, classes, names, checks, on, off);
            context.Plan.Literal($"{disabled}>");
            WriteInside(context, item, on ?? look, checks);
            context.Plan.Literal("</div>");
        }
        else
        {
            context.Plan.Literal("<div role=\"none\"");
            WriteClass(context, classes, names, checks, on, off);
            context.Plan.Literal(">");
            WriteInside(context, item, on ?? look, checks);
            context.Plan.Literal("</div>");
        }
    }

    /// <summary>
    /// The flag of a toggle item whose <c>IsChecked</c> is bound to the model and its complement, read where the plan
    /// writes its check, or nothing for an item whose check is written as it is.
    /// </summary>
    private static (Slot On, Slot Off)? Checks(HtmlContext context, MenuItem item)
    {
        if (!context.Plan.Planning || item.ToggleType == MenuItemToggleType.None)
        {
            return null;
        }

        var negated = context.Plan.NegatedPath(item, MenuItem.IsCheckedProperty);
        return (negated ?? context.Plan.BoundPath(item, MenuItem.IsCheckedProperty)) is { } path
            ? (Flags.Of(path, negated is not null, fallback: false), Flags.Of(path, negated is not null, fallback: false, complement: true))
            : null;
    }

    /// <summary>An item's classes: its look's, or for an item whose bound check changes its look, each look under its state.</summary>
    private static void WriteClass(HtmlContext context, string classes, string? names, (Slot On, Slot Off)? checks, MenuItemLook? on, MenuItemLook? off)
    {
        if (names is not null)
        {
            context.Plan.Literal($" class=\"{names}\"");
            return;
        }

        context.Plan.Literal($" class=\"{classes} ");
        Checked(context, checks, true, on!.Classes, off!.Classes);
        context.Plan.Literal("\"");
    }

    private static void WriteRole(HtmlContext context, MenuItem item, (Slot On, Slot Off)? checks)
    {
        context.Plan.Literal(item.ToggleType switch
        {
            MenuItemToggleType.CheckBox => "role=\"menuitemcheckbox\" aria-checked=\"",
            MenuItemToggleType.Radio => "role=\"menuitemradio\" aria-checked=\"",
            _ => "role=\"menuitem\"",
        });
        if (item.ToggleType == MenuItemToggleType.None)
        {
            return;
        }

        Checked(context, checks, item.IsChecked, "true", "false");
        context.Plan.Literal("\"");
    }

    /// <summary>Writes one text for a checked item and another for an unchecked one, under the bound check's slots or as the item is.</summary>
    private static void Checked(HtmlContext context, (Slot On, Slot Off)? checks, bool current, string on, string off)
    {
        if (checks is not { } bound)
        {
            context.Plan.Literal(current ? on : off);
            return;
        }

        context.Plan.Add(new ConditionNode(bound.On.Getter, on.Length == 0 ? [] : [new LiteralNode(on)]) { Source = bound.On.Spec });
        context.Plan.Add(new ConditionNode(bound.Off.Getter, off.Length == 0 ? [] : [new LiteralNode(off)]) { Source = bound.Off.Spec });
    }

    private static void WriteInside(HtmlContext context, MenuItem item, MenuItemLook? look, (Slot On, Slot Off)? checks = null)
    {
        if (look is not null)
        {
            Checked(context, checks, true, look.Glyph ?? string.Empty, string.Empty);
        }
        else if (item.ToggleType != MenuItemToggleType.None)
        {
            Checked(context, checks, item.IsChecked, InputWriters.IconSvg("check", 16, 2), "<span class=\"mic\"></span>");
        }

        WriteIcon(context, item, look);
        context.Plan.Literal("<span class=\"mh\">");
        WriteHeader(context, item);
        context.Plan.Literal("</span>");
        if (item.InputGesture is { } gesture)
        {
            var style = look?.Gesture is { } css ? $" style=\"{css}\"" : string.Empty;
            context.Plan.Literal($"<kbd class=\"kg\"{style}>{CssValues.Encode(gesture.ToString("p", null))}</kbd>");
        }
    }

    private static void WriteIcon(HtmlContext context, MenuItem item, MenuItemLook? look)
    {
        var style = look?.Icon is { } css ? $" style=\"{css}\"" : string.Empty;
        switch (item.Icon)
        {
            case Icon glyph:
                context.Plan.Literal(style.Length > 0 ? $"<span class=\"mic\"{style}>{InputWriters.IconSvg(glyph.Kind, 16, glyph.StrokeWidth)}</span>" : InputWriters.IconSvg(glyph.Kind, 16, glyph.StrokeWidth));
                break;
            case Control icon:
                context.Plan.Literal($"<span class=\"mic\"{style}>");
                context.Write(icon, ParentKind.Row);
                context.Plan.Literal("</span>");
                break;
        }
    }

    private static void WriteHeader(HtmlContext context, MenuItem item)
    {
        if (item.Header is Control header)
        {
            context.Write(header, ParentKind.Row);
        }
        else if (LocalBindings.IsBound(item, HeaderedSelectingItemsControl.HeaderProperty))
        {
            context.Plan.Value(item, HeaderedSelectingItemsControl.HeaderProperty);
        }
        else
        {
            context.Plan.Text(AccessText(item.Header?.ToString()));
        }
    }

    private static string? AccessText(string? header)
    {
        if (header is null || !header.Contains('_', StringComparison.Ordinal))
        {
            return header;
        }

        var text = new StringBuilder(header.Length);
        var marked = false;
        var index = 0;
        while (index < header.Length)
        {
            var doubled = header[index] == '_' && index + 1 < header.Length && header[index + 1] == '_';
            if (doubled || header[index] != '_' || marked)
            {
                text.Append(header[index]);
            }
            else
            {
                marked = true;
            }

            index += doubled ? 2 : 1;
        }

        return text.ToString();
    }

    private static string Disabled(MenuItem item) => item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true) ? string.Empty : " disabled";

    /// <summary>The look of a menu Avalonia did not show (a command bar's overflow): Fluent's resources.</summary>
    public static string PopoverLook(HtmlContext context, Control owner)
    {
        var background = ThemeResources.Css(owner, "MenuFlyoutPresenterBackground", "#fff");
        var border = ThemeResources.Css(owner, "MenuFlyoutPresenterBorderBrush", "rgba(0,0,0,.1)");
        var foreground = ThemeResources.Css(owner, "MenuFlyoutItemForeground", "inherit");
        var hover = ThemeResources.Css(owner, "MenuFlyoutItemBackgroundPointerOver", "rgba(0,0,0,.05)");
        var muted = ThemeResources.Css(owner, "MenuFlyoutItemKeyboardAcceleratorTextForeground", "inherit");
        var radius = ThemeResources.Css(owner, "OverlayCornerRadius", "8px");
        var name = "mn" + CssValues.Hash(background + border + foreground + hover + muted + radius);
        context.Sheet.AddRule(name,
            $".menu.{name}[popover]{{background:{background};border:1px solid {border};color:{foreground};border-radius:{radius};padding:4px;min-width:210px;box-shadow:0 12px 32px rgba(0,0,0,.2)}}.{name} .mi:hover:not(:disabled),.{name} .mi:focus-visible,.{name} .mi:has(+:popover-open){{background:{hover}}}" +
            $".{name} .mi{{width:100%;border:0;text-align:start;font-size:14px}}.{name} .mi:disabled,.{name} .mi[aria-disabled]{{opacity:.45;cursor:default}}.{name} .mi>span:not(.mic){{flex:1}}.{name} .mic{{display:inline-flex;width:16px;justify-content:center}}" +
            $".{name} .kg{{font-family:inherit;font-weight:inherit;font-size:12px;color:{muted};margin-left:24px}}.{name} .sub{{opacity:.7;margin-left:12px}}\n");
        return name;
    }
}
