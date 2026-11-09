namespace Aioros.Avalonia.Tests.Models;

public sealed record Price(DateTime Day, double Value);

public sealed record Sale(string Region, double Amount);

public sealed record Candle(DateTime Day, double Open, double High, double Low, double Close);

public sealed record Share(string Name, double Part);

public sealed class Market
{
    public string Title { get; init; } = "Revenue";

    public IReadOnlyList<Price> Prices { get; init; } = Series(10, 20, 30, 40);

    public IReadOnlyList<Price> Target { get; init; } = Series(15, 15, 15, 15);

    public IReadOnlyList<Sale> Sales { get; init; } = [new("North", 10), new("South", 20), new("East", 40)];

    public IReadOnlyList<Candle> Candles { get; init; } =
    [
        new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), 10, 14, 9, 13),
        new(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), 13, 15, 10, 11),
        new(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Unspecified), 11, 16, 11, 15),
    ];

    public IReadOnlyList<Share> Shares { get; init; } = [new("Web", 50), new("Desktop", 30), new("Mobile", 20)];

    public static IReadOnlyList<Price> Series(params double[] values) =>
        [.. values.Select((value, index) => new Price(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(index), value))];
}

public sealed class Reading
{
    public DateTime Day { get; init; }

    public double Value { get; set; }
}

public sealed class LiveMarket
{
    public List<Reading> Prices { get; } = [.. Enumerable.Range(0, 4).Select(index => new Reading { Day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(index), Value = 10 * (index + 1) })];
}
