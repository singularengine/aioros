using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Aioros.Avalonia.Html.Writers;

internal static class RangeWriters
{
    private const string Indeterminate = "@keyframes pbi{from{background-position:-70% 0}to{background-position:170% 0}}\n"
        + "@keyframes pbv{from{background-position:0 170%}to{background-position:0 -70%}}\n";

    public static void WriteSlider(HtmlContext context, Slider slider, ParentKind parent)
    {
        var style = context.Box(slider, parent);
        var vertical = slider.Orientation == Orientation.Vertical;
        var step = slider.IsSnapToTickEnabled && slider.TickFrequency > 0 ? CssValues.Num(slider.TickFrequency) : "any";
        var classes = SliderLooks.Class(context.Sheet, slider, vertical);
        style.Append(vertical && double.IsNaN(slider.Height) && context.MinHeight(slider) > 0 ? $"min-height:{CssValues.Px(context.MinHeight(slider))};" : string.Empty);
        context.Plan.Literal("<input type=\"range\" name=\"f");
        context.Field(slider, RangeGroups.Key(slider));
        context.Plan.Literal($"\" min=\"{CssValues.Num(slider.Minimum)}\" max=\"{CssValues.Num(slider.Maximum)}\" step=\"{step}\" value=\"");
        context.Plan.Value(slider, RangeBase.ValueProperty, ValueFormat.CssNumber);
        context.Plan.Literal("\"");
        context.Invalid(slider);
        context.Root(slider, classes, style);
        if (BoundStyles.Slots(slider).Count == 0)
        {
            context.Plan.Literal(" style=\"--v:");
            context.Plan.Value(slider, RangeBase.ValueProperty, ValueFormat.CssNumber);
            context.Plan.Literal("\"");
        }

        context.Plan.Literal($"{RangeParts.Disabled(slider)}>");
    }

    public static void WriteProgress(HtmlContext context, ProgressBar bar, ParentKind parent)
    {
        var style = context.Box(bar, parent);
        var progress = ProgressClass(context.Sheet, bar);
        if (!bar.ShowProgressText)
        {
            WriteProgressElement(context, bar, progress, style);
            return;
        }

        var text = RangeParts.Part<LayoutTransformControl>(bar, "PART_LayoutTransformControl")?.Child as TextBlock;
        BoxStyles.Font(style, text?.FontSize ?? bar.FontSize, text?.FontWeight ?? bar.FontWeight, text?.Foreground ?? bar.Foreground);
        style.Append("display:grid;align-items:center;justify-items:center;");
        context.Sheet.AddRule("pbt", ".pbt>*{grid-area:1/1}.pbt>progress{align-self:stretch;justify-self:stretch;height:auto}\n");
        context.Plan.Literal("<div");
        context.Root(bar, "pbt", style, attributes: false);
        context.Plan.Literal(">");
        WriteProgressElement(context, bar, progress, null);
        context.Plan.Literal("<span>");
        if (bar.IsIndeterminate)
        {
            context.Plan.Text(ProgressText(bar).Apply(bar.Value));
        }
        else
        {
            RangeParts.Slot(context, bar, RangeBase.ValueProperty, ProgressText(bar));
        }

        context.Plan.Literal("</span></div>");
    }

    /// <summary>The <c>progress</c> element: the control's root when <paramref name="style"/> is given, else inside the root that also holds its text.</summary>
    private static void WriteProgressElement(HtmlContext context, ProgressBar bar, string progress, StringBuilder? style)
    {
        if (bar.IsIndeterminate)
        {
            context.Plan.Literal("<progress");
        }
        else
        {
            var minimum = bar.Minimum;
            context.Plan.Literal($"<progress max=\"{CssValues.Num(bar.Maximum - minimum)}\" value=\"");
            RangeParts.Slot(context, bar, RangeBase.ValueProperty, Slots.FormatOf("offset", CssValues.Num(minimum), bar.Maximum.ToString("R", CultureInfo.InvariantCulture)));
            context.Plan.Literal("\"");
        }

        if (style is null)
        {
            context.Attributes(bar);
            context.Plan.Literal($" class=\"{progress}\"");
        }
        else
        {
            context.Root(bar, progress, style);
        }

        context.Plan.Literal("></progress>");
    }

    private static Format ProgressText(ProgressBar bar) =>
        Slots.FormatOf("progress-text", null, bar.Minimum.ToString("R", CultureInfo.InvariantCulture), bar.Maximum.ToString("R", CultureInfo.InvariantCulture), bar.ProgressTextFormat ?? "{1:0}%");

    private static string ProgressClass(StyleSheet sheet, ProgressBar bar)
    {
        var vertical = bar.Orientation == Orientation.Vertical;
        var (track, value) = (RangeParts.Color(bar.Background), RangeParts.Color(bar.Foreground));
        var radius = CssValues.Px(bar.CornerRadius.TopLeft);
        sheet.AddRule("pbi", Indeterminate);
        var css = new StringBuilder("{appearance:none;-webkit-appearance:none;display:block;border:0;overflow:hidden;");
        css.Append(vertical
            ? $"writing-mode:vertical-lr;direction:rtl;width:{CssValues.Px(bar.MinWidth)};height:100%;min-height:{CssValues.Px(bar.MinHeight)};"
            : $"width:100%;height:{CssValues.Px(bar.MinHeight)};");
        css.Append(CultureInfo.InvariantCulture, $"border-radius:{radius};background:{track};color:{value}}}");
        css.Append(CultureInfo.InvariantCulture, $"$::-webkit-progress-bar{{background:transparent}}$::-webkit-progress-value{{background:{value};border-radius:{radius}}}");
        css.Append(CultureInfo.InvariantCulture, $"$::-moz-progress-bar{{background:{value};border-radius:{radius}}}");
        css.Append(CultureInfo.InvariantCulture, $"$:indeterminate{{background:linear-gradient({value},{value}) 0 0/{(vertical ? "100% 40%" : "40% 100%")} no-repeat,{track};");
        css.Append(CultureInfo.InvariantCulture, $"animation:{(vertical ? "pbv" : "pbi")} 2s cubic-bezier(.4,0,.6,1) infinite}}$:indeterminate::-moz-progress-bar{{background:transparent}}");
        return Rule(sheet, "pb", css.ToString());
    }

    private static string Rule(StyleSheet sheet, string prefix, string css)
    {
        var name = prefix + CssValues.Hash(css);
        sheet.AddRule(name, "." + name + css.Replace("$", "." + name, StringComparison.Ordinal) + "\n");
        return name;
    }
}
