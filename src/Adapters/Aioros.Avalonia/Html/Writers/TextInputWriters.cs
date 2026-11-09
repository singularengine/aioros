using Aioros.Markup;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class TextInputWriters
{
    private static readonly ValueFormatter Invariant = value => value switch
    {
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private const string NoSpin = "nsp";

    public static void WriteTextBox(HtmlContext context, TextBox box, ParentKind parent)
    {
        var area = box.AcceptsReturn ? Rule(context, "ta", ".ta{field-sizing:content;resize:none}\n")
            : double.IsNaN(box.Width) ? Rule(context, "fs", "input.fs{field-sizing:content}\n") : null;
        var classes = FieldLooks.Classes(context, box, parent, box, null, area);
        var type = box.PasswordChar != '\0' ? "password" : Web.GetInputType(box) ?? "text";
        Open(context, box, box.AcceptsReturn ? "<textarea" : $"<input type=\"{type}\"", classes);
        Flags(context, box, box.PlaceholderText, box.MaxLength, TextBox.IsReadOnlyProperty);
        if (!box.AcceptsReturn)
        {
            context.Plan.Literal(" value=\"");
            context.Plan.Value(box, TextBox.TextProperty);
            context.Plan.Literal("\">");
            return;
        }

        context.Plan.Literal(box.TextWrapping == TextWrapping.NoWrap ? " wrap=\"off\">" : ">");
        context.Plan.Value(box, TextBox.TextProperty);
        context.Plan.Literal("</textarea>");
    }

    public static void WriteMaskedTextBox(HtmlContext context, MaskedTextBox box, ParentKind parent)
    {
        var blank = (MaskedTextProvider)box.MaskProvider!.Clone();
        blank.Clear();
        var prompt = blank.ToDisplayString();
        RequireLiteral(context, box, MaskedTextBox.MaskProperty);
        var showsMask = string.IsNullOrEmpty(box.PlaceholderText);
        var mask = showsMask && CssValues.Brush(box.Foreground) is { } color ? Rule(context, "mk" + CssValues.Hash(color), $"input.mk{CssValues.Hash(color)}::placeholder{{color:{color}}}\n") : null;
        var classes = FieldLooks.Classes(context, box, parent, box, null, mask);
        Open(context, box, "<input type=\"text\"", classes);
        Flags(context, box, showsMask ? prompt : box.PlaceholderText, box.MaxLength, TextBox.IsReadOnlyProperty);
        context.Plan.Literal($" pattern=\"{CssValues.Encode(MaskPatterns.Of(box.Mask!, box.Culture ?? CultureInfo.CurrentCulture))}\" value=\"");
        TextWriters.WriteValue(context, box, TextBox.TextProperty, Slots.FormatOf("except", prompt));
        context.Plan.Literal("\">");
    }

    public static void WriteNumericUpDown(HtmlContext context, NumericUpDown number, ParentKind parent)
    {
        RequireLiteral(context, number, NumericUpDown.MinimumProperty, NumericUpDown.MaximumProperty, NumericUpDown.IncrementProperty);
        ThemeLooks.ExpandTemplates(number);
        var field = number.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() ?? new TextBox();
        var frame = new Frame(number.Background, number.BorderBrush, number.BorderThickness, number.CornerRadius, number.Padding);
        var spin = SpinClass(context, number.ShowButtonSpinner, number.Padding);
        Open(context, number, "<input type=\"number\"", FieldLooks.Classes(context, number, parent, field, frame, spin));
        context.Plan.Literal(number.Minimum != decimal.MinValue ? $" min=\"{Invariant(number.Minimum)}\"" : string.Empty);
        context.Plan.Literal(number.Maximum != decimal.MaxValue ? $" max=\"{Invariant(number.Maximum)}\"" : string.Empty);
        context.Plan.Literal(" step=\"any\"");
        Flags(context, number, number.PlaceholderText, 0, NumericUpDown.IsReadOnlyProperty);
        context.Plan.Literal(" value=\"");
        TextWriters.WriteValue(context, number, NumericUpDown.ValueProperty, Slots.FormatOf("invariant"));
        context.Plan.Literal("\">");
    }

    public static void WriteAutoCompleteBox(HtmlContext context, AutoCompleteBox box, ParentKind parent)
    {
        ThemeLooks.ExpandTemplates(box);
        var field = box.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() ?? new TextBox();
        var path = context.Plan.PathOf(box, AutoCompleteBox.ItemsSourceProperty) ?? (LocalBindings.Of(box, AutoCompleteBox.ItemsSourceProperty) as IDescription)?.Description;
        var items = (box.ItemsSource ?? Array.Empty<object>()).Cast<object?>().Select(PlanValues.Format).ToList();
        var list = "dl" + CssValues.Hash(path ?? string.Join('\n', items));
        Open(context, box, "<input type=\"text\"", FieldLooks.Classes(context, box, parent, field, null, null));
        context.Plan.Literal($" list=\"{list}\"");
        Flags(context, box, box.PlaceholderText, box.MaxLength, null);
        context.Plan.Literal(" value=\"");
        context.Plan.Value(box, AutoCompleteBox.TextProperty);
        context.Plan.Literal("\">");
        context.Sheet.AddRule("acb", "input[list]::-webkit-calendar-picker-indicator{display:none!important}\n");
        if (!context.Sheet.AddRule(list, string.Empty))
        {
            return;
        }

        context.Plan.Literal($"<datalist id=\"{list}\">");
        WriteOptions(context, box, items);
        context.Plan.Literal("</datalist>");
    }

    private static void WriteOptions(HtmlContext context, AutoCompleteBox box, List<string> items)
    {
        if (context.Plan.Planning && context.Plan.BoundPath(box, AutoCompleteBox.ItemsSourceProperty) is { } bound)
        {
            var identity = Slots.FormatOf("identity");
            context.Plan.Add(new RepeatNode(PropertyPaths.Getter(bound), [new LiteralNode("<option value=\""), new ValueNode(Slots.Self.Getter, identity.Formatter) { Source = Slots.Self.Spec, FormatSpec = identity.Spec }, new LiteralNode("\">")]) { Source = SlotSpec.Bound(bound) });
        }
        else
        {
            context.Plan.Literal(string.Concat(items.Select(item => $"<option value=\"{CssValues.Encode(item)}\">")));
        }
    }

    public static void BindNumber(NumericUpDown number, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0)
        {
            number.Value = null;
        }
        else if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            var clamped = Math.Clamp(parsed, number.Minimum, number.Maximum);
            number.Value = number.ClipValueToMinMax || clamped == parsed ? clamped : number.Value;
        }
    }

    private static void Open(HtmlContext context, Control control, string open, (string Classes, StringBuilder Style) look)
    {
        context.Plan.Literal(open);
        context.Attributes(control);
        context.Plan.Literal(" name=\"f");
        context.Field(control);
        context.Plan.Literal("\"" + (control is TextBox ? ChangedFields.Delayed(control, TextBox.TextProperty) : string.Empty));
        context.Invalid(control);
        context.Root(control, look.Classes, look.Style, attributes: false);
    }

    private static void Flags(HtmlContext context, Control control, string? placeholder, int maxLength, AvaloniaProperty<bool>? readOnly)
    {
        RequireLiteral(context, control, TextBox.PlaceholderTextProperty, TextBox.MaxLengthProperty, NumericUpDown.PlaceholderTextProperty, AutoCompleteBox.PlaceholderTextProperty);
        context.Plan.Literal(string.IsNullOrEmpty(placeholder) ? string.Empty : $" placeholder=\"{CssValues.Encode(placeholder)}\"");
        context.Plan.Literal(maxLength > 0 ? string.Create(CultureInfo.InvariantCulture, $" maxlength=\"{maxLength}\"") : string.Empty);
        if (readOnly is not null && context.Plan.Planning && context.Plan.BoundPath(control, readOnly) is { } path)
        {
            context.Plan.Add(new ConditionNode(PropertyPaths.Getter(path), [new LiteralNode(" readonly")]) { Source = SlotSpec.Bound(path) });
        }
        else if (readOnly is not null && control.GetValue(readOnly) is true)
        {
            context.Plan.Literal(" readonly");
        }

        context.Plan.Literal(control.IsEffectivelyEnabled ? string.Empty : " disabled");
    }

    private static void RequireLiteral(HtmlContext context, Control control, params AvaloniaProperty[] properties)
    {
        if (context.Plan.Planning && properties.FirstOrDefault(property => context.Plan.ReadsModel(control, property)) is { } bound)
        {
            context.Plan.Fail($"{control.GetType().Name}.{bound.Name} is bound");
        }
    }

    private static string Rule(HtmlContext context, string name, string css)
    {
        context.Sheet.AddRule(name, css);
        return name;
    }

    private static string SpinClass(HtmlContext context, bool show, Thickness padding)
    {
        if (show)
        {
            var margin = $"-{CssValues.Px(padding.Top)} -{CssValues.Px(padding.Right)} -{CssValues.Px(padding.Bottom)} {CssValues.Px(padding.Right)}";
            var spin = "spn" + CssValues.Hash(margin);
            context.Sheet.AddRule(spin, $".{spin}::-webkit-inner-spin-button{{opacity:1;align-self:stretch;margin:{margin};width:24px;cursor:pointer}}\n");
            return spin;
        }

        context.Sheet.AddRule(NoSpin, $".{NoSpin}{{appearance:textfield}}.{NoSpin}::-webkit-inner-spin-button,.{NoSpin}::-webkit-outer-spin-button{{-webkit-appearance:none;margin:0}}\n");
        return NoSpin;
    }
}
