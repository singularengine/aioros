using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Panels;
using Aioros.Avalonia.Plans;

namespace Aioros.Avalonia.Charts;

internal sealed record SeriesDefinition(
    ChartKind Kind,
    string? Items,
    string? X,
    string? Y,
    string? Open,
    string? High,
    string? Low,
    string Stroke,
    string Fill,
    double Thickness);

internal sealed record ChartDefinition(
    string? Title,
    string Text,
    string Grid,
    bool Axes,
    double Width,
    double Height,
    ImmutableArray<SeriesDefinition> Series)
{
    public string Json() => JsonSerializer.Serialize(this, ChartJson.Default.ChartDefinition);

    public static ChartDefinition Parse(string json) => JsonSerializer.Deserialize(json, ChartJson.Default.ChartDefinition)!;
}

[JsonSerializable(typeof(ChartDefinition))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal sealed partial class ChartJson : JsonSerializerContext;

internal static class ChartDefinitions
{
    public const string Accent = "#5e6ad2";

    public static ChartDefinition Of(Chart chart, PlanRecorder? plan)
    {
        ArgumentNullException.ThrowIfNull(chart);
        LayOut(chart);
        var text = CssValues.Brush(chart.Foreground) ?? "#6b6f76";
        var height = Size(chart.Bounds.Height, chart.Height, 240);
        var round = chart.Series.Count > 0 && chart.Series.All(series => series.Kind == ChartKind.Donut);
        return new ChartDefinition(
            chart.Title,
            text,
            CssValues.Brush(chart.GridLines) ?? string.Empty,
            chart.ShowAxes,
            round && double.IsNaN(chart.Width) ? height : Size(chart.Bounds.Width, chart.Width, 720),
            height,
            [.. chart.Series.Select(series => Series(series, plan))]);
    }

    private static SeriesDefinition Series(ChartSeries series, PlanRecorder? plan)
    {
        var stroke = CssValues.Brush(series.Stroke) ?? CssValues.Brush(series.Fill) ?? Accent;
        return new SeriesDefinition(
            series.Kind,
            plan?.PathOf(series, ChartSeries.ItemsSourceProperty),
            series.XPath,
            series.YPath,
            series.OpenPath,
            series.HighPath,
            series.LowPath,
            stroke,
            CssValues.Brush(series.Fill) ?? stroke,
            series.StrokeThickness);
    }

    private static void LayOut(Chart chart)
    {
        if (chart.Bounds.Width > 0 || global::Avalonia.Controls.TopLevel.GetTopLevel(chart) is not { } root)
        {
            return;
        }

        var size = Viewports.Of(root);
        root.Measure(size);
        root.Arrange(new global::Avalonia.Rect(size));
    }

    private static double Size(double actual, double set, double fallback) =>
        actual > 0 ? actual : double.IsNaN(set) ? fallback : set;
}
