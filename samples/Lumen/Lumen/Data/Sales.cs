using System.Globalization;
using Aioros.Functions;
using Lumen.ViewModels;

namespace Lumen.Data;

/// <summary>The dashboard at one moment: what every visitor sees, the same for all.</summary>
public sealed record DashboardData(Kpi Revenue, Kpi Orders, Kpi Users, Kpi Conversion, Bar[] Bars, Order[] RecentOrders, double[] Traffic, string TrafficNow, string Updated);

public interface IDashboardFunctions
{
    Task<DashboardData> SnapshotAsync();
}

/// <summary>
/// The dashboard's numbers read from the server's simulated sales. One read is shared by every visitor between two
/// ticks: the feed invalidates <c>sales</c> on each tick, which evicts the read and reruns the dashboard's loader for
/// every connection that shows it.
/// </summary>
public sealed class DashboardFunctions(Sales sales) : IDashboardFunctions
{
    [Cached(1, Shared = true)]
    [Reads("sales")]
    public Task<DashboardData> SnapshotAsync() => Task.FromResult(sales.Current);
}

/// <summary>
/// The simulated sales, on the server only: the feed advances them, and readers get the snapshot of the last tick.
/// </summary>
public sealed class Sales
{
    public const int Points = 60;

    public const double BarMax = 130;

    private static readonly string[] Customers =
        ["Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton", "Dennis Ritchie",
         "Barbara Liskov", "Ken Thompson", "Frances Allen", "Donald Knuth", "Radia Perlman", "John McCarthy"];

    private readonly TimeProvider _time;
    private readonly Random _rng = new(42);
    private readonly double[] _traffic = new double[Points];
    private readonly double[] _sales = new double[12];
    private readonly List<Order> _orders = [];
    private double _revenue = 128_430, _prevRevenue = 128_430;
    private int _orderCount = 1_204, _prevOrders = 1_204, _nextOrder = 10_240;
    private double _prevConversion = 3.2;
    private DashboardData _current;

    public Sales(TimeProvider time)
    {
        _time = time;
        for (var i = 0; i < Points; i++) _traffic[i] = 520 + 120 * Math.Sin(i / 6.0) + _rng.Next(-40, 40);
        for (var i = 0; i < _sales.Length; i++) _sales[i] = 30 + _rng.Next(0, 70);
        for (var i = 0; i < 8; i++) _orders.Add(NewOrder());
        _current = Next();
    }

    public DashboardData Current => Volatile.Read(ref _current);

    /// <summary>Advances the simulation one tick; only the feed calls it.</summary>
    public void Tick() => Volatile.Write(ref _current, Next());

    private DashboardData Next()
    {
        Array.Copy(_traffic, 1, _traffic, 0, Points - 1);
        _traffic[^1] = Math.Clamp(_traffic[^2] + _rng.Next(-60, 61), 150, 950);
        var hour = _rng.Next(_sales.Length);
        _sales[hour] = Math.Clamp(_sales[hour] + _rng.Next(-15, 16), 5, 100);

        if (_rng.NextDouble() < 0.4)
        {
            var o = NewOrder();
            _orders.Insert(0, o);
            if (_orders.Count > 8) _orders.RemoveAt(8);
            _orderCount++;
            _revenue += double.Parse(o.Amount.TrimStart('$'), CultureInfo.InvariantCulture);
        }
        var conversion = Math.Round(Math.Clamp(_prevConversion + (_rng.NextDouble() - 0.5) * 0.2, 1.5, 6), 2);

        var data = new DashboardData(
            new Kpi("Revenue", Money(_revenue), Pct(_revenue, _prevRevenue), _revenue >= _prevRevenue),
            new Kpi("Orders", _orderCount.ToString("N0", CultureInfo.InvariantCulture), Pct(_orderCount, _prevOrders), _orderCount >= _prevOrders),
            new Kpi("Active users", _traffic[^1].ToString("N0", CultureInfo.InvariantCulture), Pct(_traffic[^1], _traffic[^2]), _traffic[^1] >= _traffic[^2]),
            new Kpi("Conversion", conversion.ToString("0.00", CultureInfo.InvariantCulture) + "%", Pct(conversion, _prevConversion), conversion >= _prevConversion),
            _sales.Select((v, i) => new Bar($"{8 + i:00}h", Math.Round(v / 100 * BarMax, 1), $"{v * 0.42:0.0}k")).ToArray(),
            _orders.ToArray(),
            (double[])_traffic.Clone(),
            _traffic[^1].ToString("N0", CultureInfo.InvariantCulture) + " /min",
            "Updated " + _time.GetLocalNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
        (_prevRevenue, _prevOrders, _prevConversion) = (_revenue, _orderCount, conversion);
        return data;
    }

    private Order NewOrder()
    {
        var status = _rng.NextDouble() switch { < 0.7 => "paid", < 0.9 => "pending", _ => "failed" };
        var amount = 20 + _rng.Next(0, 480) + _rng.Next(0, 100) / 100.0;
        return new Order($"#{_nextOrder++}", Customers[_rng.Next(Customers.Length)], "$" + amount.ToString("0.00", CultureInfo.InvariantCulture), status);
    }

    private static string Money(double v) => "$" + v.ToString("N0", CultureInfo.InvariantCulture);

    private static string Pct(double now, double before) =>
        (before == 0 ? 0 : (now - before) / before * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
}

/// <summary>Ticks the simulated sales and invalidates <c>sales</c>, so every live dashboard reruns its loader.</summary>
public sealed class DashboardFeed(Sales sales, IDataKeys keys, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval =
        TimeSpan.FromMilliseconds(int.TryParse(Environment.GetEnvironmentVariable("LUMEN_TICK_MS"), out var ms) ? ms : 1000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            sales.Tick();
            keys.Invalidate("sales");
        }
    }
}
