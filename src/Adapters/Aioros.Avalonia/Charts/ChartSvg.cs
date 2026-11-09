using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Charts;

internal static class ChartSvg
{
    private static readonly ConditionalWeakTable<object, Drawn> Cache = [];
    private static int _built;
    private const string Placeholder = "\u0001";

    public static int Built => Volatile.Read(ref _built);

    public static ValueFormatter Formatter(ChartDefinition chart)
    {
        var key = chart.Json();
        var sources = chart.Series.Select(series => series.Items is { } path ? PropertyPaths.Getter(path) : null).ToImmutableArray();
        return scope => Draw(chart, key, [.. sources.Select(source => source?.Invoke(scope) as IEnumerable)]);
    }

    public static string Draw(ChartDefinition chart, string key, IReadOnlyList<IEnumerable?> data)
    {
        var anchor = data.FirstOrDefault(source => source is not null && !source.GetType().IsValueType);
        var values = ChartModels.Values(chart, data);
        if (anchor is not null && Cache.TryGetValue(anchor, out var drawn) && drawn.Key == key && drawn.Data.SequenceEqual(data, ReferenceEqualityComparer.Instance) && drawn.Values.SequenceEqual(values))
        {
            return drawn.Svg;
        }

        var svg = Build(chart, data);
        if (anchor is not null)
        {
            Cache.AddOrUpdate(anchor, new Drawn(key, data, values, svg));
        }

        return svg;
    }

    private static string Build(ChartDefinition chart, IReadOnlyList<IEnumerable?> data)
    {
        Interlocked.Increment(ref _built);
        var context = new SvgRenderContext(Placeholder, chart.Width, chart.Height, chart.Series.All(series => series.Kind != Controls.ChartKind.Donut));
        var model = ChartModels.Build(chart, data);
        if (ChartModels.Render(model, context, chart.Width, chart.Height) is { } error)
        {
            throw new InvalidOperationException("The chart could not be laid out: " + error.Message, error);
        }

        var body = context.Compose(model.PlotArea);
        return body.Replace(Placeholder, "c" + Html.Looks.CssValues.Hash(body), StringComparison.Ordinal);
    }

    private sealed record Drawn(string Key, IReadOnlyList<IEnumerable?> Data, ImmutableArray<object?> Values, string Svg);
}
