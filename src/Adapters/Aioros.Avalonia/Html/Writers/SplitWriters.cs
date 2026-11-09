using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class SplitWriters
{
    private const string Chevron = "<svg viewBox=\"0 0 2048 2048\" width=\"12\" height=\"12\" fill=\"currentColor\" style=\"flex-shrink:0\"><path d=\"M1939 486L2029 576L1024 1581L19 576L109 486L1024 1401L1939 486Z\"/></svg>";
    private const string Increase = "<svg viewBox=\"0 0 20 10\" width=\"16\" height=\"8\" fill=\"currentColor\"><path d=\"M0,9 L10,0 20,9 19,10 10,2 1,10 z\"/></svg>";
    private const string Decrease = "<svg viewBox=\"0 0 20 10\" width=\"16\" height=\"8\" fill=\"currentColor\"><path d=\"M0,1 L10,10 20,1 19,0 10,8 1,0 z\"/></svg>";

    public static void WriteDropDown(HtmlContext context, DropDownButton button, ParentKind parent)
    {
        var style = context.Box(button, parent);
        BoxStyles.Font(style, button.FontSize, button.FontWeight, button.Foreground);
        var look = ThemeLooks.Of(button, "RootBorder", ThemeLooks.PressStates);
        style.Append(look.Css);
        style.Append(CultureInfo.InvariantCulture, $"padding-right:10px;display:inline-flex;align-items:center;justify-content:space-between;gap:{CssValues.Px(button.Padding.Right)};");
        style.Append(button.MinHeight > 0 ? $"min-height:{CssValues.Px(button.MinHeight)};" : string.Empty);
        var states = context.Sheet.States(look);
        if (button.Flyout is null || !button.IsEffectivelyEnabled)
        {
            context.Plan.Literal("<button type=\"button\"");
            context.Root(button, states, style);
            context.Plan.Literal(button.IsEffectivelyEnabled ? ">" : " disabled>");
            context.Content(button.Content);
            context.Plan.Literal($"{Chevron}</button>");
            return;
        }

        var target = context.NextPopover();
        context.Plan.Literal($"<button type=\"button\" popovertarget=\"{target}\"");
        context.Root(button, states, style, $"anchor-name:--{target}");
        context.Plan.Literal(">");
        context.Content(button.Content);
        context.Plan.Literal($"{Chevron}</button>");
        WriteFlyout(context, target, button.Flyout, button);
    }

    public static void WriteFlyout(HtmlContext context, string id, FlyoutBase flyout, Control owner)
    {
        switch (flyout)
        {
            case MenuFlyout menu:
                FlyoutWriters.Opened(menu, owner, presenter => MenuWriters.WritePopover(context, id, menu.Items, owner, Placements.Of(menu), presenter));
                break;
            case Flyout panel:
                FlyoutWriters.Opened(panel, owner, presenter => FlyoutWriters.WriteFlyout(context, id, panel, owner, presenter));
                break;
        }
    }

    public static void WriteSplitButton(HtmlContext context, SplitButton split, ParentKind parent)
    {
        ThemeLooks.ExpandTemplates(split);
        var parts = split.GetVisualDescendants().OfType<Button>().ToList();
        var primary = parts.Find(part => part.Name == "PART_PrimaryButton");
        var secondary = parts.Find(part => part.Name == "PART_SecondaryButton");
        if (primary is null || secondary is null)
        {
            return;
        }

        var style = context.Box(split, parent);
        BoxStyles.Font(style, split.FontSize, split.FontWeight, null);
        style.Append("display:inline-flex;flex-direction:row;align-items:stretch;");
        style.Append(split.MinHeight > 0 ? $"min-height:{CssValues.Px(split.MinHeight)};" : string.Empty);
        var states = ToggleWriters.ToggleStates(context, split, "SplitButton");
        var group = GroupLook(context, split);
        var enabled = split.IsEffectivelyEnabled;
        var target = split.Flyout is not null && enabled ? context.NextPopover() : null;
        context.Plan.Literal("<div");
        context.Root(split, group, style, target is null ? null : $"anchor-name:--{target}", attributes: false);
        context.Plan.Literal(">");
        var part = split is ToggleSplitButton checkable ? TogglePart(context, checkable, primary, states, false) : context.Sheet.Class(states, PartStyle(primary, false));
        if (split is ToggleSplitButton toggle)
        {
            context.Plan.Literal($"<label class=\"{part}\">");
            ToggleWriters.WriteHiddenCheck(context, toggle, ToggleSplitButton.IsCheckedProperty);
            context.Content(split.Content);
            context.Plan.Literal("</label>");
        }
        else
        {
            context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
            context.Button(primary);
            context.Plan.Literal("|");
            context.Plan.Value(split, SplitButton.CommandParameterProperty);
            context.Plan.Literal("\"");
            context.Attributes(split);
            context.Plan.Literal($" class=\"{part}\"{(enabled ? string.Empty : " disabled")}>");
            context.Content(split.Content);
            context.Plan.Literal("</button>");
        }

        var separator = new StringBuilder("width:1px;flex:0 0 1px;border-style:solid;");
        separator.Append(CultureInfo.InvariantCulture, $"border-width:{CssValues.Px(split.BorderThickness.Top)} 0 {CssValues.Px(split.BorderThickness.Bottom)} 0;border-color:{CssValues.Brush(split.BorderBrush) ?? "transparent"};");
        context.Plan.Literal($"<span class=\"{context.Sheet.Class(null, separator)}\"></span>");
        var opens = target is null ? string.Empty : $" popovertarget=\"{target}\"";
        context.Plan.Literal($"<button type=\"button\"{opens} class=\"{(split is ToggleSplitButton both ? TogglePart(context, both, secondary, states, true) : context.Sheet.Class(states, PartStyle(secondary, true)))}\"{(enabled ? string.Empty : " disabled")}>{Chevron}</button></div>");
        if (target is not null)
        {
            WriteFlyout(context, target, split.Flyout!, split);
        }
    }

    public static void WriteButtonSpinner(HtmlContext context, ButtonSpinner spinner, ParentKind parent)
    {
        var style = context.Box(spinner, parent);
        BoxStyles.Font(style, spinner.FontSize, spinner.FontWeight, spinner.Foreground);
        BoxStyles.Fill(style, spinner.Background, spinner.BorderBrush, spinner.BorderThickness, spinner.CornerRadius, default);
        style.Append("display:inline-flex;flex-direction:row;align-items:stretch;overflow:hidden;");
        style.Append(spinner.MinHeight > 0 ? $"min-height:{CssValues.Px(spinner.MinHeight)};" : string.Empty);
        var content = new StringBuilder("flex:1 1 auto;display:flex;align-items:center;");
        content.Append(spinner.Padding != default ? $"padding:{BoxStyles.Edges(spinner.Padding)};" : string.Empty);
        var left = spinner.ButtonSpinnerLocation == Location.Left;
        context.Plan.Literal("<div");
        context.Root(spinner, null, style);
        context.Plan.Literal(">");
        if (left)
        {
            WriteSpinButtons(context, spinner, left);
        }

        context.Plan.Literal($"<div class=\"{context.Sheet.Class(null, content)}\">");
        context.Content(spinner.Content);
        context.Plan.Literal("</div>");
        if (!left)
        {
            WriteSpinButtons(context, spinner, left);
        }

        context.Plan.Literal("</div>");
    }

    public static void BindSpinner(ButtonSpinner spinner, string? value)
    {
        SpinDirection? direction = value switch
        {
            "increase" => SpinDirection.Increase,
            "decrease" => SpinDirection.Decrease,
            _ => null,
        };
        if (direction is { } spin && CanSpin(spinner, spin))
        {
            spinner.RaiseEvent(new SpinEventArgs(Spinner.SpinEvent, spin));
        }
    }

    private static bool CanSpin(ButtonSpinner spinner, SpinDirection direction) =>
        spinner.IsEffectivelyEnabled && spinner.AllowSpin && spinner.ShowButtonSpinner
        && spinner.ValidSpinDirection.HasFlag(direction == SpinDirection.Increase ? ValidSpinDirections.Increase : ValidSpinDirections.Decrease);

    private static void WriteSpinButtons(HtmlContext context, ButtonSpinner spinner, bool left)
    {
        if (!spinner.ShowButtonSpinner)
        {
            return;
        }

        var edge = $"{CssValues.Px(spinner.BorderThickness.Left)} solid {CssValues.Brush(spinner.BorderBrush) ?? "transparent"}";
        var hover = ThemeResources.Paint(spinner, "RepeatButtonBackgroundPointerOver") ?? "transparent";
        var pressed = ThemeResources.Paint(spinner, "RepeatButtonBackgroundPressed") ?? "transparent";
        var disabled = $"color:{ThemeResources.Paint(spinner, "RepeatButtonForegroundDisabled") ?? "GrayText"};background:{ThemeResources.Paint(spinner, "RepeatButtonBackgroundDisabled") ?? "transparent"}";
        var name = "sp" + CssValues.Hash($"{edge}{hover}{pressed}{disabled}{left}");
        context.Sheet.AddRule(name, $".{name}{{min-width:34px;padding:0;border:0;border-{(left ? "right" : "left")}:{edge};background:transparent;display:flex;align-items:center;justify-content:center}}"
            + $".{name}:hover:enabled{{background:{hover}}}.{name}:active:enabled{{background:{pressed}}}.{name}:disabled{{{disabled};cursor:default}}\n");
        foreach (var (value, glyph, direction) in new[] { ("increase", Increase, SpinDirection.Increase), ("decrease", Decrease, SpinDirection.Decrease) })
        {
            context.Plan.Literal("<button type=\"submit\" name=\"f");
            context.Field(spinner);
            context.Plan.Literal($"\" value=\"{value}\" class=\"{name}\"{(CanSpin(spinner, direction) ? string.Empty : " disabled")}>{glyph}</button>");
        }
    }

    /// <summary>
    /// The class of a toggle split button's part: its look at rest, whatever value the trace saw, and its checked look as a
    /// rule on the button's hidden check, both as the theme resolves them (<see cref="StateLooks"/>), so every value renders alike.
    /// </summary>
    private static string TogglePart(HtmlContext context, ToggleSplitButton toggle, Button part, string states, bool secondary)
    {
        if (part.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(presenter => presenter.Name == "PART_ContentPresenter") is not { } presenter)
        {
            return context.Sheet.Class(states, PartStyle(part, secondary));
        }

        var look = StateLooks.Resolve(toggle, [new(":checked", string.Empty)], () => ThemeLooks.Snapshot(toggle, presenter));
        var style = new StringBuilder(string.Concat(look.Rest.Select(static declaration => $"{declaration.Key}:{declaration.Value};")));
        style.Append(presenter.Padding != default ? $"padding:{BoxStyles.Edges(presenter.Padding)};" : string.Empty);
        style.Append(CultureInfo.InvariantCulture, $"position:relative;display:inline-flex;align-items:center;cursor:pointer;user-select:none;min-width:{CssValues.Px(part.MinWidth)};");
        style.Append(secondary ? "justify-content:center;" : "flex:1 1 auto;");
        var rules = string.Concat(look.Rules.Select(static rule => string.Concat(rule.Rule.Select(static declaration => $"{declaration.Key}:{declaration.Value};"))));
        var name = "ts" + CssValues.Hash(rules + secondary);
        context.Sheet.AddRule(name, rules.Length == 0 ? string.Empty : (secondary ? $":has(>label>input:checked)>.{name}" : $".{name}:has(>input:checked)") + $"{{{rules}}}\n");
        return CssValues.Join(context.Sheet.Class(states, style), name);
    }

    private static StringBuilder PartStyle(Button part, bool secondary)
    {
        var style = new StringBuilder();
        if (part.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(presenter => presenter.Name == "PART_ContentPresenter") is { } presenter)
        {
            BoxStyles.Fill(style, presenter.Background, presenter.BorderBrush, presenter.BorderThickness, presenter.CornerRadius, presenter.Padding);
            style.Append(CssValues.Brush(presenter.Foreground) is { } color ? $"color:{color};" : string.Empty);
        }

        style.Append(CultureInfo.InvariantCulture, $"position:relative;display:inline-flex;align-items:center;cursor:pointer;user-select:none;min-width:{CssValues.Px(part.MinWidth)};");
        style.Append(secondary ? "justify-content:center;" : "flex:1 1 auto;");
        return style;
    }

    private static string GroupLook(HtmlContext context, SplitButton split)
    {
        if (split is not ToggleSplitButton)
        {
            return string.Empty;
        }

        var state = split.IsEffectivelyEnabled ? "Checked" : "CheckedDisabled";
        var background = ThemeResources.Paint(split, $"SplitButtonBackground{state}") ?? "transparent";
        var border = ThemeResources.Paint(split, $"SplitButtonBorderBrush{state}") ?? "transparent";
        var foreground = ThemeResources.Paint(split, $"SplitButtonForeground{state}") ?? "inherit";
        var name = "sg" + CssValues.Hash(background + border + foreground);
        context.Sheet.AddRule(name, $".{name}:has(>label>input:checked)>button{{background:{background};border-color:{border};color:{foreground}}}\n");
        return name;
    }
}
