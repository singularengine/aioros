using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Vectors;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The looks of a customizable select's options that differ from its enabled items': its disabled options as the theme
/// draws a disabled item (a list's labels), and the check mark of the selected option as the part its item template shows
/// only while selected, drawn over the option's padding where the template places it (none when the template has none).
/// </summary>
internal static class SelectOptions
{
    /// <summary>The rule drawing disabled options as the first disabled item that shows content; nothing without one.</summary>
    public static string Disabled(ComboBox combo)
    {
        if (combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => !item.IsEnabled && item.Content is not (null or Separator)) is not { } item)
        {
            return string.Empty;
        }

        ThemeLooks.ExpandTemplates(item);
        var part = item.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == "PART_ContentPresenter");
        var css = new StringBuilder();
        BoxStyles.Fill(css, null, null, default, default, item.Padding);
        css.Append(CultureInfo.InvariantCulture, $"font-size:{CssValues.Px(item.FontSize)};");
        css.Append(part?.GetValue(TextBlock.LineHeightProperty) is { } line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        css.Append(CssValues.Brush(item.Foreground) is { } color ? $"color:{color};" : string.Empty);
        css.Append(CultureInfo.InvariantCulture, $"opacity:{CssValues.Num(item.Opacity)};");
        return $".$ option:disabled{{{css}}}";
    }

    /// <summary>The selected option's check mark: the parts of an enabled item's template shown only while it is selected, or no mark.</summary>
    public static string CheckMark(ComboBox combo, Popup host, Control child)
    {
        var mark = (string?)null;
        void Read(Control? surface)
        {
            if (surface?.GetVisualDescendants().OfType<ComboBoxItem>().FirstOrDefault(item => item.IsEnabled) is { } item)
            {
                mark = Selected(item, surface);
            }
        }

        if (child.IsAttachedToVisualTree())
        {
            Read(child);
        }
        else
        {
            host.SetCurrentValue(Popup.ChildProperty, null);
            try
            {
                Writers.Probes.With(combo, child, Read);
            }
            finally
            {
                host.SetCurrentValue(Popup.ChildProperty, child);
            }
        }

        return mark ?? ".$ option::checkmark{display:none}";
    }

    private static string? Selected(ComboBoxItem item, Control surface)
    {
        var classes = (IPseudoClasses)item.Classes;
        var was = item.Classes.Contains(":selected");
        classes.Set(":selected", false);
        Writers.Probes.Settle(surface);
        var hidden = item.GetVisualDescendants().OfType<Shape>().Where(shape => !Writers.Probes.Shows(shape, item)).ToList();
        classes.Set(":selected", true);
        foreach (var shape in hidden)
        {
            Writers.Probes.Invalidate(shape);
        }

        Writers.Probes.Settle(surface);
        try
        {
            var shown = hidden.Where(shape => Writers.Probes.Shows(shape, item)).ToList();
            if (shown.Count == 0 || shown.Select(shape => shape.TransformToVisual(item)).Any(transform => transform is null))
            {
                return null;
            }

            var boxes = shown.Select(shape => new Rect(shape.Bounds.Size).TransformToAABB(shape.TransformToVisual(item)!.Value)).ToList();
            var box = boxes.Aggregate((a, b) => a.Union(b));
            var parts = shown.Select(shape => ((Visual)shape, shape.TransformToVisual(item)!.Value * Matrix.CreateTranslation(-box.X, -box.Y), shape.Opacity));
            if (DrawingSvg.Parts(parts, box.Size) is not { } svg)
            {
                return null;
            }

            var right = item.Bounds.Width - box.Right;
            return string.Create(CultureInfo.InvariantCulture, $".$ option{{position:relative}}.$ option::checkmark{{content:{DrawingSvg.Url(svg)};position:absolute;right:{CssValues.Px(right)};top:{CssValues.Px(box.Y)};width:{CssValues.Px(box.Width)};height:{CssValues.Px(box.Height)}}}");
        }
        finally
        {
            classes.Set(":selected", was);
            Writers.Probes.Settle(surface);
        }
    }
}
