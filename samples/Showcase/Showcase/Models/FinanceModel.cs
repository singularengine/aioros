using System.Globalization;
using Aioros.Generated;
using Aioros.Functions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Showcase.Models;

public sealed record Tick(DateTime At, double Value);

public sealed record Candle(DateTime Day, double Open, double High, double Low, double Close);

public sealed record Slice(string Name, double Weight, string Share);

public sealed record Quote(string Symbol, string Name, string Price, string Change, bool Up, IReadOnlyList<Tick> History);

public sealed record Trade(string Time, string Symbol, string Side, string Size, string Price, bool Buy);

/// <summary>The finance dashboard at one moment, the same for every visitor.</summary>
public sealed record FinanceData(
    IReadOnlyList<Candle> Candles,
    IReadOnlyList<Tick> Prices,
    IReadOnlyList<Tick> Average,
    IReadOnlyList<Tick> Portfolio,
    IReadOnlyList<Quote> Watchlist,
    IReadOnlyList<Trade> Trades,
    string Price,
    string Change,
    bool Up,
    string Value,
    string DayChange,
    string Updated);

public interface IFinanceFunctions
{
    Task<FinanceData> SnapshotAsync();
}

/// <summary>The market read by the finance page: one read shared by every visitor between two ticks of the feed.</summary>
public sealed class FinanceFunctions(Market market) : IFinanceFunctions
{
    [Cached(1, Shared = true)]
    [Reads("market")]
    public Task<FinanceData> SnapshotAsync() => Task.FromResult(market.Current);
}

public sealed partial class FinanceModel : ObservableObject
{
    public FinanceModel(FinanceData data) => Receive(data);

    public string Xaml { get; } = AiorosSources.Text("Pages/finance/Page.axaml") ?? string.Empty;

    public IReadOnlyList<Slice> Allocation { get; } =
    [
        new("Technology", 42, "42%"), new("Healthcare", 18, "18%"), new("Energy", 15, "15%"),
        new("Logistics", 13, "13%"), new("Cash", 12, "12%"),
    ];

    [ObservableProperty]
    public partial IReadOnlyList<Candle> Candles { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Tick> Prices { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Tick> Average { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Tick> Portfolio { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Quote> Watchlist { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Trade> Trades { get; set; } = [];

    [ObservableProperty]
    public partial string Price { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Change { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Up { get; set; }

    [ObservableProperty]
    public partial bool Down { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DayChange { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Updated { get; set; } = string.Empty;

    public string Cash { get; } = "$48,210";

    public string Positions { get; } = "6 holdings";

    public void Receive(FinanceData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Candles = data.Candles;
        Prices = data.Prices;
        Average = data.Average;
        Portfolio = data.Portfolio;
        Watchlist = data.Watchlist;
        Trades = data.Trades;
        Price = data.Price;
        Change = data.Change;
        Up = data.Up;
        Down = !data.Up;
        Value = data.Value;
        DayChange = data.DayChange;
        Updated = data.Updated;
    }
}

/// <summary>The simulated market, on the server only: the feed advances it, and readers get the snapshot of the last tick.</summary>
public sealed class Market
{
    private static readonly DateTime Start = new(2026, 10, 9, 9, 30, 0, DateTimeKind.Unspecified);
    private static readonly (string Symbol, string Name, double Price)[] Symbols =
    [
        ("AIOR", "Aioros Inc.", 184.2), ("NBLA", "Nebula Cloud", 92.4), ("HLX", "Helix Bio", 41.7),
        ("QNT", "Quantia", 268.9), ("VRDN", "Verdant Energy", 57.3), ("ORBT", "Orbit Freight", 23.8),
    ];

    private readonly Random _random = new(42);
    private readonly double[] _prices = [.. Symbols.Select(symbol => symbol.Price)];
    private readonly List<Tick>[] _histories = [.. Symbols.Select(_ => new List<Tick>())];
    private readonly List<Tick> _portfolio = [];
    private readonly List<Trade> _trades = [];
    private readonly IReadOnlyList<Candle> _candles;
    private readonly double _open;
    private DateTime _clock = Start;
    private FinanceData _current;

    public Market()
    {
        _candles = Daily();
        for (var step = 0; step < 90; step++)
        {
            Advance();
        }

        _open = _histories[0][0].Value;
        _current = Snapshot();
    }

    public FinanceData Current => Volatile.Read(ref _current);

    /// <summary>Advances the market one minute; only the feed calls it.</summary>
    public void Tick()
    {
        Advance();
        Volatile.Write(ref _current, Snapshot());
    }

    private void Advance()
    {
        _clock = _clock.AddMinutes(1);
        for (var index = 0; index < _prices.Length; index++)
        {
            _prices[index] = Math.Max(1, _prices[index] * (1 + ((_random.NextDouble() - 0.5) * 0.006)));
            Append(_histories[index], new Tick(_clock, _prices[index]), 90);
        }

        Append(_portfolio, new Tick(_clock, _prices.Zip([120, 300, 800, 90, 400, 900], (price, shares) => price * shares).Sum()), 90);
        if (_random.NextDouble() < 0.6)
        {
            var symbol = _random.Next(Symbols.Length);
            var buy = _random.NextDouble() < 0.55;
            _trades.Insert(0, new Trade(_clock.ToString("HH:mm", CultureInfo.InvariantCulture), Symbols[symbol].Symbol, buy ? "Buy" : "Sell", (_random.Next(1, 40) * 10).ToString(CultureInfo.InvariantCulture), Money(_prices[symbol]), buy));
            if (_trades.Count > 8)
            {
                _trades.RemoveAt(_trades.Count - 1);
            }
        }
    }

    private FinanceData Snapshot()
    {
        var history = _histories[0];
        var change = (_prices[0] - _open) / _open * 100;
        var value = _portfolio[^1].Value;
        var first = _portfolio[0].Value;
        return new FinanceData(
            _candles,
            [.. history],
            [.. history.Select((tick, index) => new Tick(tick.At, history.Skip(Math.Max(0, index - 14)).Take(Math.Min(15, index + 1)).Average(point => point.Value)))],
            [.. _portfolio],
            [.. Symbols.Select((symbol, index) => Quoted(symbol.Symbol, symbol.Name, _histories[index]))],
            [.. _trades],
            Money(_prices[0]),
            change.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "% today",
            change >= 0,
            "$" + value.ToString("N0", CultureInfo.InvariantCulture),
            ((value - first) / first * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%",
            "Updated " + _clock.ToString("HH:mm", CultureInfo.InvariantCulture));
    }

    private static Quote Quoted(string symbol, string name, List<Tick> history)
    {
        var change = (history[^1].Value - history[0].Value) / history[0].Value * 100;
        return new Quote(symbol, name, Money(history[^1].Value), change.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%", change >= 0, [.. history.TakeLast(30)]);
    }

    private static void Append(List<Tick> ticks, Tick tick, int limit)
    {
        ticks.Add(tick);
        if (ticks.Count > limit)
        {
            ticks.RemoveAt(0);
        }
    }

    private List<Candle> Daily()
    {
        var close = 150.0;
        var candles = new List<Candle>();
        for (var day = 0; day < 40; day++)
        {
            var open = close;
            close = open * (1 + ((_random.NextDouble() - 0.46) * 0.04));
            candles.Add(new Candle(Start.Date.AddDays(day - 40), open, Math.Max(open, close) * (1 + (_random.NextDouble() * 0.012)), Math.Min(open, close) * (1 - (_random.NextDouble() * 0.012)), close));
        }

        return candles;
    }

    private static string Money(double value) => "$" + value.ToString("N2", CultureInfo.InvariantCulture);
}

/// <summary>Moves the market once a second and invalidates <c>market</c>, so every live finance page reruns its loader.</summary>
public sealed class MarketFeed(Market market, IDataKeys keys, TimeProvider time) : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            market.Tick();
            keys.Invalidate("market");
        }
    }
}
