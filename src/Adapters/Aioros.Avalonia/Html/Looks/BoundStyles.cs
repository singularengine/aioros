using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// Bound values written as the element's inline style, so each render writes the model's value: the size, the margin,
/// the opacity, a single rotate, scale, translate, skew or matrix transform or a bound render transform, a frame's
/// background, padding and corner radius, a border's shadow, and the text color and size of text and of controls with
/// text content. Only elements whose writer opens them with <see cref="HtmlContext.Root"/> (containers, text, icons and
/// the rooted control writers) take them, and their class leaves those declarations out; elsewhere the value stays in
/// the class and the plan reports the binding it does not follow.
/// </summary>
internal static class BoundStyles
{
    public static bool Inline(Control control) => ControlWriters.For(control) is not { Rooted: false };

    /// <summary>Whether a brush is bound to the model, or shown by a template part from its control's binding (<c>{TemplateBinding Background}</c>).</summary>
    public static bool Bound(Control control, AvaloniaProperty property) => LocalBindings.IsBound(control, property) || TemplateBindings.Bound(control, property) is not null;

    public static bool Opacity(Control control) => LocalBindings.IsBound(control, Visual.OpacityProperty) && Inline(control);

    public static bool Margin(Control control) =>
        LocalBindings.IsBound(control, Layoutable.MarginProperty) && !BoxStyles.CentersByMaxWidth(control) && Inline(control);

    public static bool Shadow(Control control) => control is Border && LocalBindings.IsBound(control, Border.BoxShadowProperty) && Inline(control);

    public static bool Padding(Control control) => FramePart(control, Decorator.PaddingProperty, TemplatedControl.PaddingProperty) is { } property && LocalBindings.IsBound(control, property);

    public static bool Radius(Control control) => FramePart(control, Border.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty) is { } property && LocalBindings.IsBound(control, property);

    public static bool Transform(Control control) => Transformed(control) is not null && Inline(control);

    public static bool Any(Control control) => Slots(control).Count > 0;

    /// <summary>Whether a bound brush on the control is written in its inline style, so the plan follows it.</summary>
    public static bool Paints(Control control, AvaloniaProperty property) =>
        (property == TextElement.ForegroundProperty && Foreground(control)) || (Background(control) is { } background && background == property);

    public static List<InlineSlot> Slots(Control control)
    {
        var slots = new List<InlineSlot>();
        void Add(bool bound, string name, AvaloniaProperty? property, ValueFormat format, string suffix)
        {
            if (bound)
            {
                slots.Add(new InlineSlot(name, property, format, suffix));
            }
        }

        Add(LocalBindings.IsBound(control, Layoutable.HeightProperty), "height", Layoutable.HeightProperty, ValueFormat.CssNumber, "px");
        Add(LocalBindings.IsBound(control, Layoutable.WidthProperty), "width", Layoutable.WidthProperty, ValueFormat.CssNumber, "px");
        var background = Background(control);
        Add(background is not null && Bound(control, background), "background", background, ValueFormat.Color, string.Empty);
        Add(Bound(control, TextElement.ForegroundProperty) && Foreground(control), "color", TextElement.ForegroundProperty, ValueFormat.Color, string.Empty);
        Add(Margin(control), "margin", Layoutable.MarginProperty, ValueFormat.Thickness, string.Empty);
        Add(Opacity(control), "opacity", Visual.OpacityProperty, ValueFormat.CssNumber, string.Empty);
        Add(Shadow(control), "box-shadow", Border.BoxShadowProperty, ValueFormat.Shadow, string.Empty);
        var padding = FramePart(control, Decorator.PaddingProperty, TemplatedControl.PaddingProperty);
        Add(padding is not null && LocalBindings.IsBound(control, padding), "padding", padding, ValueFormat.Thickness, string.Empty);
        var radius = FramePart(control, Border.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty);
        Add(radius is not null && LocalBindings.IsBound(control, radius), "border-radius", radius, ValueFormat.CornerRadius, string.Empty);
        Add(Transform(control), "transform", null, ValueFormat.Transform, string.Empty);
        Add(LocalBindings.IsBound(control, TextElement.FontSizeProperty) && (control is TextBlock || TextContent(control)), "font-size", TextElement.FontSizeProperty, ValueFormat.CssNumber, "px");
        slots.AddRange(Tracks(control));
        return slots.Count == 0 || Inline(control) ? slots : [];
    }

    /// <summary>A grid's bound row heights and column widths, each written as a custom property its tracks read (<see cref="Track"/>).</summary>
    private static IEnumerable<InlineSlot> Tracks(Control control) => control is Grid grid
        ? grid.RowDefinitions.Select((definition, index) => (definition, index)).Where(static row => LocalBindings.IsBound(row.definition, RowDefinition.HeightProperty))
            .Select(static row => new InlineSlot(Track("r", row.index), RowDefinition.HeightProperty, ValueFormat.Track, string.Empty) { Target = row.definition })
            .Concat(grid.ColumnDefinitions.Select((definition, index) => (definition, index)).Where(static column => LocalBindings.IsBound(column.definition, ColumnDefinition.WidthProperty))
                .Select(static column => new InlineSlot(Track("c", column.index), ColumnDefinition.WidthProperty, ValueFormat.Track, string.Empty) { Target = column.definition }))
        : [];

    /// <summary>The custom property of a grid's bound track: <c>--r3</c> for its fourth row, <c>--c0</c> for its first column.</summary>
    public static string Track(string axis, int index) => "--" + axis + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The bound declarations of the control's whole box (its size, margin, opacity and transform), or of the rest, for a control whose whole box is an element of its own.</summary>
    public static List<InlineSlot> Outer(List<InlineSlot> slots, bool outer) =>
        [.. slots.Where(slot => (slot.Name is "height" or "width" or "margin" or "opacity" or "transform") == outer)];

    public static void Write(HtmlContext context, Control control, List<InlineSlot> slots)
    {
        var separator = string.Empty;
        foreach (var slot in slots)
        {
            if (slot.Property is { } property)
            {
                context.Plan.Literal(separator + slot.Name + ":");
                context.Plan.Value(slot.Target ?? control, property, slot.Format);
                context.Plan.Literal(slot.Suffix);
            }
            else
            {
                WriteTransform(context, control, separator);
            }

            separator = ";";
        }
    }

    private static void WriteTransform(HtmlContext context, Control control, string separator)
    {
        if (Transformed(control) is not { } transform)
        {
            return;
        }

        context.Plan.Literal(separator + "transform:" + transform.Open);
        for (var index = 0; index < transform.Parts.Length; index++)
        {
            context.Plan.Literal(index == 0 ? string.Empty : ",");
            context.Plan.Value(transform.Target, transform.Parts[index].Property, transform.Parts[index].Format);
            context.Plan.Literal(transform.Parts[index].Unit);
        }

        context.Plan.Literal(transform.Close);
    }

    /// <summary>The control's class style without the declarations its inline style writes, so the class is the same whatever the model's value.</summary>
    public static StringBuilder Strip(StringBuilder style, List<InlineSlot> slots)
    {
        if (slots.Count == 0)
        {
            return style;
        }

        var names = slots.Select(slot => slot.Name).ToHashSet(StringComparer.Ordinal);
        var kept = new StringBuilder(style.Length);
        var css = style.ToString();
        var (start, depth) = (0, 0);
        for (var index = 0; index < css.Length; index++)
        {
            depth += css[index] switch { '(' => 1, ')' => -1, _ => 0 };
            if (css[index] != ';' || depth != 0)
            {
                continue;
            }

            var declaration = css[start..(index + 1)];
            var colon = declaration.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0 || !names.Contains(declaration[..colon]))
            {
                kept.Append(declaration);
            }

            start = index + 1;
        }

        return kept.Append(css[start..]);
    }

    private static StyledProperty<IBrush?>? Background(Control control) => control switch
    {
        Border => Border.BackgroundProperty,
        Panel => Panel.BackgroundProperty,
        TemplatedControl when Frames(control) => TemplatedControl.BackgroundProperty,
        _ => null,
    };

    private static bool Foreground(Control control) => control is TextBlock or Icon || TextContent(control);

    /// <summary>
    /// A rooted content control whose content is text written in its element (a control in its content writes its own
    /// color and size), so a bound text color or size inherits as Avalonia's inherited properties do.
    /// </summary>
    private static bool TextContent(Control control) =>
        control is ContentControl { Content: null or string } and not SplitButton && ControlWriters.For(control) is { Rooted: true };

    /// <summary>A control whose element is its frame, carrying its background, border, padding and corner radius as Avalonia's template does.</summary>
    private static bool Frames(Control control) =>
        control is (Button or TextBox or ComboBox) and not (CheckBox or RadioButton or ToggleSwitch)
        && ControlWriters.For(control) is { Rooted: true };

    private static AvaloniaProperty? FramePart(Control control, AvaloniaProperty border, AvaloniaProperty templated) => control switch
    {
        Border when Inline(control) => border,
        ComboBox when templated == TemplatedControl.PaddingProperty => null,
        TemplatedControl when Frames(control) => templated,
        _ => null,
    };

    private static (string Open, AvaloniaObject Target, (AvaloniaProperty Property, ValueFormat Format, string Unit)[] Parts, string Close)? Transformed(Control control) => control.RenderTransform switch
    {
        _ when LocalBindings.IsBound(control, Visual.RenderTransformProperty) =>
            (string.Empty, control, [(Visual.RenderTransformProperty, ValueFormat.Transform, string.Empty)], string.Empty),
        RotateTransform { CenterX: 0, CenterY: 0 } rotate when LocalBindings.IsBound(rotate, RotateTransform.AngleProperty) =>
            ("rotate(", rotate, [(RotateTransform.AngleProperty, ValueFormat.CssNumber, "deg")], ")"),
        ScaleTransform scale when LocalBindings.IsBound(scale, ScaleTransform.ScaleXProperty) || LocalBindings.IsBound(scale, ScaleTransform.ScaleYProperty) =>
            ("scale(", scale, [(ScaleTransform.ScaleXProperty, ValueFormat.CssNumber, string.Empty), (ScaleTransform.ScaleYProperty, ValueFormat.CssNumber, string.Empty)], ")"),
        TranslateTransform translate when LocalBindings.IsBound(translate, TranslateTransform.XProperty) || LocalBindings.IsBound(translate, TranslateTransform.YProperty) =>
            ("translate(", translate, [(TranslateTransform.XProperty, ValueFormat.CssNumber, "px"), (TranslateTransform.YProperty, ValueFormat.CssNumber, "px")], ")"),
        SkewTransform skew when LocalBindings.IsBound(skew, SkewTransform.AngleXProperty) || LocalBindings.IsBound(skew, SkewTransform.AngleYProperty) =>
            ("matrix(1,", skew, [(SkewTransform.AngleYProperty, ValueFormat.Tangent, string.Empty), (SkewTransform.AngleXProperty, ValueFormat.Tangent, string.Empty)], ",1,0,0)"),
        MatrixTransform matrix when LocalBindings.IsBound(matrix, MatrixTransform.MatrixProperty) =>
            (string.Empty, matrix, [(MatrixTransform.MatrixProperty, ValueFormat.Matrix, string.Empty)], string.Empty),
        _ => null,
    };
}

internal readonly record struct InlineSlot(string Name, AvaloniaProperty? Property, ValueFormat Format, string Suffix)
{
    /// <summary>The object whose property the slot writes, when it is not the control itself (a grid's row definition).</summary>
    public AvaloniaObject? Target { get; init; }
}
