using System.Globalization;
using Aioros.Avalonia.Charts;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Html.Writers;

internal static class ChartWriters
{
    public static void WriteChart(HtmlContext context, Chart chart, ParentKind parent)
    {
        var style = context.Box(chart, parent);
        var definition = ChartDefinitions.Of(chart, context.Plan.Planning ? context.Plan : null);
        style.Append(CultureInfo.InvariantCulture, $"position:relative;height:{CssValues.Px(definition.Height)};");
        context.Sheet.AddRule("cl", ".cl{position:absolute;line-height:1.21;white-space:nowrap;pointer-events:none}.cc{translate:-50% 0}.cr{translate:-100% 0}\n");
        context.Plan.Literal($"<div class=\"{context.Classes(chart, null, style)}\" role=\"img\" aria-label=\"");
        context.Plan.Value(chart, Chart.TitleProperty);
        context.Plan.Literal("\">");
        if (context.Plan.Planning && chart.Series.Any(series => LocalBindings.IsBound(series, ChartSeries.ItemsSourceProperty)))
        {
            Slot(context, chart, definition);
        }
        else
        {
            context.Plan.Literal(ChartSvg.Draw(definition, definition.Json(), [.. chart.Series.Select(series => series.ItemsSource)]));
        }

        context.Plan.Literal("</div>");
    }

    private static void Slot(HtmlContext context, Chart chart, ChartDefinition definition)
    {
        if (definition.Series.Any(series => series.Items is null))
        {
            context.Plan.Fail("a chart series is not bound to a simple path of the current scope");
            context.Plan.Literal(ChartSvg.Draw(definition, definition.Json(), [.. chart.Series.Select(series => series.ItemsSource)]));
            return;
        }

        var format = Slots.FormatOf("chart", definition.Json());
        context.Plan.Add(new ValueNode(Slots.Self.Getter, format.Formatter, Raw: true) { Source = Slots.Self.Spec, FormatSpec = format.Spec });
    }
}
