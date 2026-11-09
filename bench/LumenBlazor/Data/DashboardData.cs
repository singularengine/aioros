using System.Globalization;

namespace LumenBlazor.Data;

public sealed record Kpi(string Name, string Value, string Delta, bool IsUp);
public sealed record Bar(string Label, double Height, string Value);
public sealed record Order(string Id, string Customer, string Amount, string Status);

/// Same simulated feed as the POC's DashboardViewModel (same seed, sizes and update rules).
public sealed class DashboardData
{
    public const int Points = 60;
    public const double ChartWidth = 600, ChartHeight = 180, BarMax = 130;

    private static readonly string[] Customers =
        ["Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton", "Dennis Ritchie",
         "Barbara Liskov", "Ken Thompson", "Frances Allen", "Donald Knuth", "Radia Perlman", "John McCarthy"];

    private readonly Random _rng = new(42);
    private readonly double[] _traffic = new double[Points];
    private readonly double[] _sales = new double[12];
    private readonly List<Order> _orders = [];
    private double _revenue = 128_430, _prevRevenue = 128_430;
    private int _orderCount = 1_204, _prevOrders = 1_204, _nextOrder = 10_240;
    private double _prevConversion = 3.2;

    public Kpi RevenueKpi { get; private set; } = null!;
    public Kpi OrdersKpi { get; private set; } = null!;
    public Kpi UsersKpi { get; private set; } = null!;
    public Kpi ConversionKpi { get; private set; } = null!;
    public Bar[] Bars { get; private set; } = [];
    public string TrafficLine { get; private set; } = "";
    public string TrafficArea { get; private set; } = "";
    public Order[] RecentOrders { get; private set; } = [];
    public string TrafficNow { get; private set; } = "";
    public string Updated { get; private set; } = "";

    public event Action? Changed;

    public DashboardData()
    {
        for (var i = 0; i < Points; i++) _traffic[i] = 520 + 120 * Math.Sin(i / 6.0) + _rng.Next(-40, 40);
        for (var i = 0; i < _sales.Length; i++) _sales[i] = 30 + _rng.Next(0, 70);
        for (var i = 0; i < 8; i++) _orders.Add(NewOrder());
        Tick();
    }

    public void Tick()
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

        RevenueKpi = new Kpi("Revenue", "$" + _revenue.ToString("N0", CultureInfo.InvariantCulture), Pct(_revenue, _prevRevenue), _revenue >= _prevRevenue);
        OrdersKpi = new Kpi("Orders", _orderCount.ToString("N0", CultureInfo.InvariantCulture), Pct(_orderCount, _prevOrders), _orderCount >= _prevOrders);
        UsersKpi = new Kpi("Active users", _traffic[^1].ToString("N0", CultureInfo.InvariantCulture), Pct(_traffic[^1], _traffic[^2]), _traffic[^1] >= _traffic[^2]);
        ConversionKpi = new Kpi("Conversion", conversion.ToString("0.00", CultureInfo.InvariantCulture) + "%", Pct(conversion, _prevConversion), conversion >= _prevConversion);
        (_prevRevenue, _prevOrders, _prevConversion) = (_revenue, _orderCount, conversion);

        var pts = Enumerable.Range(0, Points)
            .Select(i => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(i * ChartWidth / (Points - 1), 2)},{Math.Round(ChartHeight - _traffic[i] / 1000 * (ChartHeight - 10), 2)}"))
            .ToArray();
        TrafficLine = string.Join(" ", pts);
        TrafficArea = TrafficLine + $" {ChartWidth},{ChartHeight} 0,{ChartHeight}";
        TrafficNow = _traffic[^1].ToString("N0", CultureInfo.InvariantCulture) + " /min";

        Bars = _sales.Select((v, i) => new Bar($"{8 + i:00}h", Math.Round(v / 100 * BarMax, 1), $"{v * 0.42:0.0}k")).ToArray();
        RecentOrders = _orders.ToArray();
        Updated = "Updated " + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        Changed?.Invoke();
    }

    private Order NewOrder()
    {
        var status = _rng.NextDouble() switch { < 0.7 => "paid", < 0.9 => "pending", _ => "failed" };
        var amount = 20 + _rng.Next(0, 480) + _rng.Next(0, 100) / 100.0;
        return new Order($"#{_nextOrder++}", Customers[_rng.Next(Customers.Length)], "$" + amount.ToString("0.00", CultureInfo.InvariantCulture), status);
    }

    private static string Pct(double now, double before) =>
        (before == 0 ? 0 : (now - before) / before * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
}

public sealed class DashboardTicker(DashboardData data) : BackgroundService
{
    private static readonly TimeSpan Interval =
        TimeSpan.FromMilliseconds(int.TryParse(Environment.GetEnvironmentVariable("POC_TICK_MS"), out var ms) ? ms : 1000);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(ct)) data.Tick();
    }
}
