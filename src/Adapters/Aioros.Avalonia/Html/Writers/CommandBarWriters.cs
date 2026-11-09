using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Html.Writers;

internal static class CommandBarWriters
{
    private const string More = "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"16\" height=\"16\" fill=\"currentColor\"><circle cx=\"6\" cy=\"12\" r=\"2\"/><circle cx=\"12\" cy=\"12\" r=\"2\"/><circle cx=\"18\" cy=\"12\" r=\"2\"/></svg>";

    private const string Css =
        ".cb{display:flex;flex-direction:row;align-items:center;justify-content:flex-end;gap:2px;list-style:none;margin:0}.cb>li{display:flex;align-self:stretch;align-items:center}" +
        ".cb>.cbc{margin-right:auto}.cbs{width:1px;min-height:24px;margin:4px 8px}.cbx{position:absolute;opacity:0;width:1px;height:1px;pointer-events:none}" +
        ".cbb{display:inline-flex;flex-direction:column;align-items:center;justify-content:center;gap:4px;cursor:pointer;flex-shrink:0}.cbb>.cbl{font-size:11px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:100%}" +
        ".cbb.cbr{flex-direction:row;gap:8px}.cbb.cbr>.cbl{font-size:inherit}.cbb:has(:focus-visible){outline:2px solid;outline-offset:-2px}.cbb:disabled{opacity:.45;cursor:default}\n";


    public static void WriteBar(HtmlContext context, CommandBar bar, ParentKind parent)
    {
        var style = context.Box(bar, parent);
        BoxStyles.Fill(style, bar.Background, bar.BorderBrush, bar.BorderThickness, bar.CornerRadius, bar.Padding);
        style.Append(bar.MinHeight > 0 ? $"min-height:{CssValues.Px(bar.MinHeight)};" : string.Empty);
        context.Sheet.AddRule("cb", Css);
        context.Plan.Literal($"<menu class=\"{context.Classes(bar, "cb", style)}\">");
        if (bar.Content is not null)
        {
            context.Plan.Literal("<li class=\"cbc\">");
            context.Content(bar.Content);
            context.Plan.Literal("</li>");
        }

        FlyoutWriters.Inherit([.. bar.PrimaryCommands.OfType<Control>(), .. bar.SecondaryCommands.OfType<Control>()], bar.DataContext);
        foreach (var command in bar.PrimaryCommands.OfType<Control>().Where(command => command.IsVisible))
        {
            WriteCommand(context, command);
        }

        WriteOverflow(context, bar);
        context.Plan.Literal("</menu>");
    }

    public static void WriteButton(HtmlContext context, CommandBarButton button, ParentKind parent)
    {
        var classes = Classes(context, button, button.LabelPosition, button.IsCompact, parent);
        context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
        context.Button(button);
        context.Plan.Literal("|");
        context.Plan.Value(button, Button.CommandParameterProperty);
        context.Plan.Literal($"\" class=\"{classes}\"{(Enabled(button) ? string.Empty : " disabled")}>");
        WriteFace(context, button.Icon, button.Label ?? button.Content?.ToString(), button.IsCompact || button.LabelPosition == CommandBarDefaultLabelPosition.Collapsed);
        context.Plan.Literal("</button>");
    }

    public static void WriteToggle(HtmlContext context, CommandBarToggleButton toggle, ParentKind parent)
    {
        var classes = Classes(context, toggle, toggle.LabelPosition, toggle.IsCompact, parent);
        var accent = ThemeResources.Css(toggle, "SystemControlHighlightAccentBrush", "#0078d4");
        var name = "cbt" + CssValues.Hash(accent);
        context.Sheet.AddRule(name, $".{name}:has(:checked){{background:{accent}!important}}\n");
        context.Plan.Literal($"<label class=\"{CssValues.Join(classes, name)}\"><input type=\"checkbox\" class=\"cbx\" name=\"f");
        context.Field(toggle);
        context.Plan.Literal("\" value=\"on\"");
        if (context.Plan.Planning && context.Plan.BoundPath(toggle, ToggleButton.IsCheckedProperty) is { } path)
        {
            context.Plan.Add(new ConditionNode(PropertyPaths.Getter(path), [new LiteralNode(" checked")]) { Source = SlotSpec.Bound(path) });
        }
        else if (toggle.IsChecked == true)
        {
            context.Plan.Literal(" checked");
        }

        context.Plan.Literal(Enabled(toggle) ? ">" : " disabled>");
        WriteFace(context, toggle.Icon, toggle.Label ?? toggle.Content?.ToString(), toggle.IsCompact || toggle.LabelPosition == CommandBarDefaultLabelPosition.Collapsed);
        context.Plan.Literal("</label>");
    }

    public static void WriteSeparator(HtmlContext context, CommandBarSeparator separator, ParentKind parent)
    {
        context.Sheet.AddRule("cb", Css);
        context.Plan.Literal($"<div role=\"separator\" class=\"cbs\" style=\"background:{SeparatorColor(separator)}\"></div>");
    }

    private static void WriteCommand(HtmlContext context, Control command)
    {
        if (command is CommandBarSeparator separator)
        {
            context.Plan.Literal($"<li role=\"separator\" class=\"cbs\" style=\"background:{SeparatorColor(separator)}\"></li>");
            return;
        }

        context.Plan.Literal("<li>");
        context.Write(command, ParentKind.Row);
        context.Plan.Literal("</li>");
    }

    private static void WriteOverflow(HtmlContext context, CommandBar bar)
    {
        var secondary = bar.SecondaryCommands.OfType<Control>().Where(command => command.IsVisible).ToList();
        if (secondary.Count == 0 || bar.OverflowButtonVisibility == CommandBarOverflowButtonVisibility.Collapsed)
        {
            return;
        }

        var id = context.NextPopover();
        var look = MenuWriters.PopoverLook(context, bar);
        context.Plan.Literal($"<li><button type=\"button\" class=\"cbb cbm\" popovertarget=\"{id}\" style=\"anchor-name:--{id};padding:0 12px;min-width:40px;align-self:stretch;background:none;border:0\" aria-label=\"More\">{More}</button>");
        context.Plan.Literal($"<div popover id=\"{id}\" class=\"menu {look}\" style=\"position-anchor:--{id};top:anchor(bottom);right:anchor(right);margin-top:4px;\">");
        foreach (var command in secondary)
        {
            WriteOverflowItem(context, command);
        }

        context.Plan.Literal("</div></li>");
    }

    private static void WriteOverflowItem(HtmlContext context, Control command)
    {
        switch (command)
        {
            case CommandBarSeparator:
                context.Plan.Literal("<hr>");
                break;
            case CommandBarButton button:
                context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
                context.Button(button);
                context.Plan.Literal("|");
                context.Plan.Value(button, Button.CommandParameterProperty);
                context.Plan.Literal($"\" class=\"mi\"{(Enabled(button) ? string.Empty : " disabled")}>");
                WriteMenuFace(context, button.Icon, button.Label ?? button.Content?.ToString());
                context.Plan.Literal("</button>");
                break;
            case CommandBarToggleButton toggle:
                context.Plan.Literal("<label class=\"mi\"><input type=\"checkbox\" name=\"f");
                context.Field(toggle);
                context.Plan.Literal($"\" value=\"on\"{(toggle.IsChecked == true ? " checked" : string.Empty)}>");
                WriteMenuFace(context, null, toggle.Label ?? toggle.Content?.ToString());
                context.Plan.Literal("</label>");
                break;
            default:
                context.Write(command, ParentKind.Column);
                break;
        }
    }

    private static void WriteFace(HtmlContext context, object? icon, string? label, bool compact)
    {
        WriteIcon(context, icon, icon is Icon glyph ? glyph.Size : 20);
        if (!compact && label is not null)
        {
            context.Plan.Literal($"<span class=\"cbl\">{CssValues.Encode(label)}</span>");
        }
    }

    private static void WriteMenuFace(HtmlContext context, object? icon, string? label)
    {
        WriteIcon(context, icon, 16);
        context.Plan.Literal($"<span>{CssValues.Encode(label)}</span>");
    }

    private static void WriteIcon(HtmlContext context, object? icon, double size)
    {
        switch (icon)
        {
            case Icon glyph:
                context.Plan.Literal(InputWriters.IconSvg(glyph.Kind, size, glyph.StrokeWidth));
                break;
            case Control control:
                context.Write(control, ParentKind.Row);
                break;
        }
    }

    private static string Classes(HtmlContext context, Button button, CommandBarDefaultLabelPosition position, bool compact, ParentKind parent)
    {
        context.Sheet.AddRule("cb", Css);
        var style = context.Box(button, parent, size: false);
        var look = Unchecked(button);
        style.Append(look.Css);
        BoxStyles.Font(style, button.FontSize, button.FontWeight, button.Foreground);
        var right = position == CommandBarDefaultLabelPosition.Right;
        var width = compact ? 40 : button.Width;
        style.Append(right || double.IsNaN(width) ? "min-width:40px;" : $"width:{CssValues.Px(width)};");
        style.Append(button.MinHeight > 0 ? $"min-height:{CssValues.Px(button.MinHeight)};" : string.Empty);
        var classes = CssValues.Join(right ? "cbb cbr" : "cbb", context.Sheet.States(look));
        return context.Classes(button, classes, style);
    }

    private static Look Unchecked(Button button)
    {
        var classes = (IPseudoClasses)button.Classes;
        var isChecked = button.Classes.Contains(":checked");
        classes.Remove(":checked");
        var look = ThemeLooks.Of(button, "PART_Border", ThemeLooks.PressStates);
        classes.Set(":checked", isChecked);
        return look;
    }

    private static string SeparatorColor(Control separator) => ThemeResources.Css(separator, "SystemControlForegroundBaseMediumLowBrush", "rgba(0,0,0,.4)");

    private static bool Enabled(Button button) => button.IsEnabled && (button.Command?.CanExecute(button.CommandParameter) ?? true);
}
