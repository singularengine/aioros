using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Aioros.Avalonia.Charts;

internal static class ChartModels
{
    private const string HorizontalKey = "x";

    private const string VerticalKey = "y";

    private static readonly string[] Palette = ["#5e6ad2", "#26b5ce", "#f2994a", "#4cb782", "#eb5757", "#9b51e0"];

    private static ImmutableDictionary<string, ValueGetter> _getters = ImmutableDictionary<string, ValueGetter>.Empty.WithComparers(StringComparer.Ordinal);

    public static PlotModel Build(ChartDefinition chart, IReadOnlyList<IEnumerable?> data)
    {
        var model = new PlotModel
        {
            TextColor = Color(chart.Text),
            PlotAreaBorderThickness = new OxyThickness(0),
            Padding = chart.Axes ? new OxyThickness(4, 8, 8, 4) : new OxyThickness(0),
            DefaultFont = "Inter",
            DefaultFontSize = 11,
            IsLegendVisible = false,
        };
        var items = chart.Series.Select((series, index) => Items(index < data.Count ? data[index] : null)).ToList();
        if (chart.Series.Any(series => series.Kind != ChartKind.Donut))
        {
            AddAxes(model, chart, items);
        }

        for (var index = 0; index < chart.Series.Length; index++)
        {
            model.Series.Add(Series(chart.Series[index], items[index], index));
        }

        return model;
    }

    public static Exception? Render(PlotModel model, IRenderContext context, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(model);
        IPlotModel plot = model;
        plot.Update(true);
        if (model.GetLastPlotException() is { } error)
        {
            return error;
        }

        plot.Render(context, new OxyRect(0, 0, width, height));
        return null;
    }

    private static List<object> Items(IEnumerable? source) => source is null ? [] : [.. source.Cast<object>()];

    /// <summary>
    /// Every value the chart reads from its data, in order, with each series' item count: two drawings of the same
    /// definition are the same exactly when these are equal. A value that is not a scalar is taken as its text, which is
    /// what a category axis or a slice label shows.
    /// </summary>
    public static ImmutableArray<object?> Values(ChartDefinition chart, IReadOnlyList<IEnumerable?> data)
    {
        var values = ImmutableArray.CreateBuilder<object?>();
        for (var index = 0; index < chart.Series.Length; index++)
        {
            var series = chart.Series[index];
            var items = Items(index < data.Count ? data[index] : null);
            values.Add(items.Count);
            foreach (var item in items)
            {
                foreach (var path in (string?[])[series.X, series.Y, series.Open, series.High, series.Low])
                {
                    values.Add(path is null ? null : Scalar(Read(item, path)));
                }
            }
        }

        return values.ToImmutable();
    }

    private static object? Scalar(object? value) => value is null or IConvertible or ValueType ? value : value.ToString();

    private static void AddAxes(PlotModel model, ChartDefinition chart, List<List<object>> items)
    {
        var first = chart.Series.Select((series, index) => (series, index)).First(pair => pair.series.Kind != ChartKind.Donut);
        var sample = items[first.index].Select(item => Read(item, first.series.X)).FirstOrDefault(value => value is not null);
        var columns = first.series.Kind == ChartKind.Column;
        Axis horizontal = sample switch
        {
            _ when columns => new CategoryAxis { ItemsSource = items[first.index].Select(item => Read(item, first.series.X)?.ToString() ?? string.Empty).ToList(), GapWidth = 0.4 },
            DateTime or DateTimeOffset or DateOnly => new DateTimeAxis { StringFormat = Span(items[first.index], first.series.X) < 2 ? "HH:mm" : "MMM d" },
            string => new CategoryAxis { ItemsSource = items[first.index].Select(item => Read(item, first.series.X) as string ?? string.Empty).ToList() },
            _ => new LinearAxis(),
        };
        var vertical = new LinearAxis
        {
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = chart.Grid.Length == 0 ? OxyColor.FromAColor(36, Color(chart.Text)) : Color(chart.Grid),
            MinimumPadding = 0.05,
            MaximumPadding = 0.1,
        };
        horizontal.Key = HorizontalKey;
        vertical.Key = VerticalKey;
        if (columns)
        {
            vertical.MinimumPadding = 0;
            vertical.AbsoluteMinimum = 0;
        }

        foreach (var (axis, position) in ((Axis, AxisPosition)[])[(horizontal, AxisPosition.Bottom), (vertical, AxisPosition.Left)])
        {
            axis.Position = position;
            axis.TickStyle = TickStyle.None;
            axis.AxislineStyle = LineStyle.None;
            axis.IsAxisVisible = chart.Axes;
            axis.IsZoomEnabled = false;
            axis.IsPanEnabled = false;
            model.Axes.Add(axis);
        }
    }

    private static double Span(List<object> items, string? path)
    {
        var times = items.Select(item => X(Read(item, path))).Where(double.IsFinite).ToList();
        return times.Count < 2 ? 0 : times.Max() - times.Min();
    }

    private static Series Series(SeriesDefinition series, List<object> items, int index) => series.Kind switch
    {
        ChartKind.Area => Area(series, items),
        ChartKind.Column => Column(series, items),
        ChartKind.Candle => Candles(series, items),
        ChartKind.Donut => Donut(series, items, index),
        _ => Line(new LineSeries(), series, items),
    };

    private static LineSeries Line(LineSeries line, SeriesDefinition series, List<object> items)
    {
        line.Color = Color(series.Stroke);
        line.StrokeThickness = series.Thickness;
        line.Points.AddRange(items.Select(item => new DataPoint(X(Read(item, series.X)), Number(Read(item, series.Y)))));
        return line;
    }

    private static AreaSeries Area(SeriesDefinition series, List<object> items)
    {
        var area = (AreaSeries)Line(new AreaSeries(), series, items);
        area.Fill = OxyColor.FromAColor(48, Color(series.Fill));
        area.Color2 = OxyColors.Transparent;
        var values = area.Points.Select(point => point.Y).Where(double.IsFinite).ToList();
        area.ConstantY2 = values.Count == 0 ? 0 : values.Min();
        return area;
    }

    private static BarSeries Column(SeriesDefinition series, List<object> items)
    {
        var column = new BarSeries { FillColor = Color(series.Fill), StrokeThickness = 0, XAxisKey = VerticalKey, YAxisKey = HorizontalKey };
        column.Items.AddRange(items.Select(item => new BarItem(Number(Read(item, series.Y)))));
        return column;
    }

    private static CandleStickSeries Candles(SeriesDefinition series, List<object> items)
    {
        var candles = new CandleStickSeries
        {
            IncreasingColor = Color(series.Fill),
            DecreasingColor = Color(series.Stroke),
            Color = OxyColor.FromAColor(160, Color(series.Stroke)),
            StrokeThickness = 1,
        };
        candles.Items.AddRange(items.Select(item => new HighLowItem(
            X(Read(item, series.X)),
            Number(Read(item, series.High)),
            Number(Read(item, series.Low)),
            Number(Read(item, series.Open)),
            Number(Read(item, series.Y)))));
        return candles;
    }

    private static PieSeries Donut(SeriesDefinition series, List<object> items, int index)
    {
        var donut = new PieSeries { Diameter = 0.62, InnerDiameter = 0.38, StrokeThickness = 0, AngleSpan = 360, StartAngle = -90, OutsideLabelFormat = "{1}", InsideLabelFormat = string.Empty, TickHorizontalLength = 4, TickRadialLength = 4 };
        var slice = 0;
        foreach (var item in items)
        {
            donut.Slices.Add(new PieSlice(Read(item, series.X)?.ToString() ?? string.Empty, Number(Read(item, series.Y))) { Fill = Color(Palette[(index + slice++) % Palette.Length]) });
        }

        return donut;
    }

    private static object? Read(object item, string? path) =>
        path is null ? null : ImmutableInterlocked.GetOrAdd(ref _getters, path, PropertyPaths.Getter)(item);

    private static double X(object? value) => value switch
    {
        DateTime time => DateTimeAxis.ToDouble(time),
        DateTimeOffset time => DateTimeAxis.ToDouble(time.DateTime),
        DateOnly day => DateTimeAxis.ToDouble(day.ToDateTime(TimeOnly.MinValue)),
        _ => Number(value),
    };

    private static double Number(object? value) => value is IConvertible convertible ? convertible.ToDouble(CultureInfo.InvariantCulture) : double.NaN;

    public static OxyColor Color(string css) => OxyColor.Parse(css);
}
