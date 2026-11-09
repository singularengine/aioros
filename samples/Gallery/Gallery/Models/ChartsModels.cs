namespace Gallery.Models;

public sealed record Point(DateTime Day, double Value);

public sealed record Bar(string Label, double Value);

public sealed record Ohlc(DateTime Day, double Open, double High, double Low, double Close);

public sealed class ChartsModel
{
    private static readonly DateTime Start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public IReadOnlyList<Point> Revenue { get; } = Walk(7, 120, 60, 3);

    public IReadOnlyList<Point> Target { get; } = [.. Enumerable.Range(0, 60).Select(day => new Point(Start.AddDays(day), 120 + (day * 0.6)))];

    public IReadOnlyList<Bar> Regions { get; } = [new("North", 42), new("South", 31), new("East", 58), new("West", 24), new("Online", 66)];

    public IReadOnlyList<Ohlc> Stock { get; } = Candles(11, 30);

    public IReadOnlyList<Bar> Platforms { get; } = [new("Web", 54), new("Desktop", 28), new("Mobile", 18)];

    public IReadOnlyList<Point> Spark { get; } = Walk(5, 50, 30, 2);

    private static List<Point> Walk(int seed, double start, int days, double step)
    {
        var random = new Random(seed);
        var value = start;
        return [.. Enumerable.Range(0, days).Select(day => new Point(Start.AddDays(day), value += (random.NextDouble() * 2 - 0.9) * step))];
    }

    private static List<Ohlc> Candles(int seed, int days)
    {
        var random = new Random(seed);
        var close = 100.0;
        var candles = new List<Ohlc>();
        for (var day = 0; day < days; day++)
        {
            var open = close;
            close = open + (random.NextDouble() * 6 - 2.8);
            candles.Add(new Ohlc(Start.AddDays(day), open, Math.Max(open, close) + random.NextDouble() * 2, Math.Min(open, close) - random.NextDouble() * 2, close));
        }

        return candles;
    }
}
