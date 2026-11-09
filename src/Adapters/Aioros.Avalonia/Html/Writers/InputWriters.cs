using System.Globalization;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Writers;

internal static partial class InputWriters
{
    public static void WriteCheckBox(HtmlContext context, CheckBox check, ParentKind parent)
    {
        if (ChoiceCards.Card(check) is { } card)
        {
            ChoiceCards.Write(context, check, parent, card, look => WriteCheckInput(context, check, look + ">"));
            return;
        }

        var (style, own) = StateLooks.OwnStyle(check, MarkLooks.States(check, "input"), () =>
        {
            var box = context.Box(check, parent);
            BoxStyles.Font(box, check.FontSize, check.FontWeight, check.Foreground);
            return box;
        });
        var mark = MarkLooks.Class(context, check, style, own: own);
        style.Append(mark is null ? NativeCheck(check) : string.Empty);
        style.Append(mark is not null ? string.Empty : context.MinHeight(check) > 0 ? $"min-height:{CssValues.Px(context.MinHeight(check))};" : "min-height:32px;");
        context.Plan.Literal("<label");
        context.Root(check, mark, style, attributes: false);
        context.Plan.Literal(">");
        WriteCheckInput(context, check, mark is null ? $" style=\"width:20px;height:20px;flex:0 0 20px;margin:0;accent-color:{Accent(check)}\">" : ">");
        if (mark is null)
        {
            context.Plan.Text(check.Content?.ToString());
        }
        else
        {
            context.Content(check);
        }

        context.Plan.Literal("</label>");
    }

    /// <summary>A check box's native input: its field, its hot key as a page shortcut, its state, then <paramref name="end"/>, which closes the tag.</summary>
    private static void WriteCheckInput(HtmlContext context, CheckBox check, string end)
    {
        context.Plan.Literal("<input type=\"checkbox\"");
        context.Attributes(check);
        context.Plan.Literal(HotKeys.Attributes(check));
        context.Plan.Literal(" name=\"f");
        context.Field(check);
        context.Plan.Literal("\" value=\"on\"");
        context.Invalid(check);
        if (context.Plan.Planning && context.Plan.BoundPath(check, ToggleButton.IsCheckedProperty) is { } path)
        {
            context.Plan.Add(new ConditionNode(PropertyPaths.Getter(path), [new LiteralNode(" checked")]) { Source = SlotSpec.Bound(path) });
        }
        else if (check.IsChecked == true)
        {
            context.Plan.Literal(" checked");
        }

        context.Plan.Literal(check.IsEffectivelyEnabled ? string.Empty : " disabled");
        context.Plan.Literal(end);
    }

    /// <summary>The label of a check box whose theme draws no mark the web follows, around the browser's own box.</summary>
    private static string NativeCheck(CheckBox check)
    {
        var state = check.IsChecked == true ? "Checked" : "Unchecked";
        var disabled = check.IsEffectivelyEnabled ? string.Empty : $"color:{ThemeResources.Paint(check, $"CheckBoxForeground{state}Disabled") ?? "GrayText"};";
        return disabled + "display:flex;flex-direction:row;align-items:center;gap:8px;cursor:pointer;";
    }

    private static string Accent(CheckBox check) =>
        check.TryFindResource("SystemAccentColor", check.ActualThemeVariant, out var resource) && resource is Color color ? CssValues.Color(color) : "auto";

    /// <summary>The select's options: fixed or from its bound items, the selected one following its bound item or index.</summary>
    private static SelectNode Options(HtmlContext context, ComboBox combo, string? items)
    {
        var path = context.Plan.Planning ? context.Plan.BoundPath(combo, SelectingItemsControl.SelectedItemProperty) : null;
        var index = context.Plan.Planning ? context.Plan.BoundPath(combo, SelectingItemsControl.SelectedIndexProperty) : null;
        var options = items is null ? (Slot?)null : Slots.Path(items);
        var shown = OptionMember(combo);
        var text = options is null || shown is null ? (Slot?)null : Slots.Path(shown);
        return new SelectNode(path is null ? null : PropertyPaths.Getter(path), combo.SelectedIndex, [.. combo.Items.Select(OptionText)])
        {
            Source = path is null ? null : SlotSpec.Bound(path),
            Items = options?.Getter,
            ItemsSource = options?.Spec,
            Text = text?.Getter,
            TextSource = text?.Spec,
            Index = index is null ? null : PropertyPaths.Getter(index),
            IndexSource = index is null ? null : SlotSpec.Bound(index),
            Placeholder = string.IsNullOrEmpty(combo.PlaceholderText) ? null : combo.PlaceholderText,
            Labels = [.. combo.Items.Select(item => OptionLabel(item, shown))],
            Kinds = [.. combo.Items.Select(OptionKind)],
        };
    }

    public static void WriteSelect(HtmlContext context, ComboBox combo, ParentKind parent)
    {
        var style = context.Box(combo, parent);
        BoxStyles.Font(style, combo.FontSize, combo.FontWeight, combo.Foreground);
        var look = SelectLooks.Of(context, combo, style);
        if (look is null)
        {
            BoxStyles.Fill(style, combo.Background, combo.BorderBrush, combo.BorderThickness, combo.CornerRadius, combo.Padding);
        }

        style.Append(double.IsNaN(combo.Height) && LineSpacing.Line(combo, combo.FontSize) is { } line
            ? $"height:{CssValues.Px(Math.Max(context.MinHeight(combo), line + combo.Padding.Top + combo.Padding.Bottom + combo.BorderThickness.Top + combo.BorderThickness.Bottom))};"
            : context.MinHeight(combo) > 0 ? $"min-height:{CssValues.Px(context.MinHeight(combo))};" : string.Empty);
        var items = context.Plan.Planning && LocalBindings.IsBound(combo, ItemsControl.ItemsSourceProperty)
            ? context.Plan.BoundPath(combo, ItemsControl.ItemsSourceProperty)
            : null;
        context.Plan.Literal("<select name=\"f");
        context.Field(combo);
        context.Plan.Literal($"\"{ItemParts.PostsChange(combo)}");
        context.Invalid(combo);
        context.Root(combo, look, style);
        context.Plan.Literal(combo.IsEffectivelyEnabled ? ">" : " disabled>");
        context.Plan.Literal(SelectButtons.Element(combo, "Up"));
        context.Plan.Add(Options(context, combo, items));
        context.Plan.Literal(SelectButtons.Element(combo, "Down"));
        context.Plan.Literal("</select>");
    }

    /// <summary>
    /// The member each item shows: the path of the combo's <c>DisplayMemberBinding</c>, or of the one text its item
    /// template binds; nothing when the item shows its own text or what its template writes cannot be named by a path.
    /// </summary>
    private static string? OptionMember(ComboBox combo)
    {
        var display = combo.DisplayMemberBinding switch
        {
            global::Avalonia.Data.ReflectionBinding { Converter: null, StringFormat: null } reflected => reflected.Path,
            global::Avalonia.Data.CompiledBinding { Converter: null, StringFormat: null } compiled => compiled.Path?.ToString(),
            _ => null,
        };
        if (display is not null && SimplePath().IsMatch(display))
        {
            return display;
        }

        if (combo.ItemTemplate is not { } template || combo.Items.FirstOrDefault(item => item is not (null or Control)) is not { } first || template.Build(first) is not { } built)
        {
            return null;
        }

        var texts = built.GetSelfAndLogicalDescendants().OfType<TextBlock>().ToList();
        return texts.Count == 1 && LocalBindings.Of(texts[0], TextBlock.TextProperty) is { } binding && BindingFormats.Converter(binding) is null && BindingFormats.StringFormat(binding) is null
            && binding is global::Avalonia.IDescription { Description: { } path } && SimplePath().IsMatch(path) ? path : null;
    }

    private static string OptionLabel(object? item, string? member) => item switch
    {
        ComboBoxItem { Content: Control content } => string.Concat(content.GetSelfAndLogicalDescendants().OfType<TextBlock>().Select(text => text.Text)),
        ComboBoxItem container => PlanValues.Format(container.Content),
        _ when member is not null && item is not null => PlanValues.Format(PropertyPaths.Getter(member)(item)),
        _ => OptionText(item),
    };

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial System.Text.RegularExpressions.Regex SimplePath();

    /// <summary>A fixed item that cannot be chosen: a separator when it shows nothing (or a <c>Separator</c>), else a disabled option.</summary>
    private static string OptionKind(object? item) => item switch
    {
        Separator => SelectNode.Separator,
        ComboBoxItem { IsEnabled: false, Content: null or Separator } => SelectNode.Separator,
        ComboBoxItem { IsEnabled: false } => SelectNode.Disabled,
        _ => string.Empty,
    };

    public static string OptionText(object? item) => PlanValues.Format(item is ComboBoxItem container ? container.Content : item);

    public static void WriteIcon(HtmlContext context, Icon icon, ParentKind parent)
    {
        var style = context.Box(icon, parent, size: false);
        style.Append(CultureInfo.InvariantCulture, $"flex-shrink:0;fill:none;stroke:currentColor;stroke-width:{CssValues.Num(icon.StrokeWidth)};stroke-linecap:round;stroke-linejoin:round;");
        if (icon.IsSet(TextElement.ForegroundProperty) && !LocalBindings.IsBound(icon, TextElement.ForegroundProperty) && CssValues.Brush(icon.Foreground) is { } foreground)
        {
            style.Append(CultureInfo.InvariantCulture, $"color:{foreground};");
        }

        context.Plan.Literal($"<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"{CssValues.Num(icon.Size)}\" height=\"{CssValues.Num(icon.Size)}\"");
        context.Root(icon, null, style, attributes: false);
        context.Plan.Literal(">");
        context.Plan.Value(icon, Icon.KindProperty, ValueFormat.Icon);
        context.Plan.Literal("</svg>");
    }

    public static string IconSvg(string? kind, double size, double stroke, string? cssClass = null) =>
        $"<svg{(cssClass is null ? string.Empty : $" class=\"{cssClass}\"")} viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"{CssValues.Num(size)}\" height=\"{CssValues.Num(size)}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"{CssValues.Num(stroke)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{Runtime.Slots.IconSet.Markup(kind)}</svg>";
}
