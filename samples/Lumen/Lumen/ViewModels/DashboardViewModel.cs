using Avalonia;
using Lumen.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.ViewModels;

public sealed record Kpi(string Name, string Value, string Delta, bool IsUp)
{
    public bool IsDown => !IsUp;
}

public sealed record Bar(string Label, double Height, string Value);

public sealed record Order(string Id, string Customer, string Amount, string Status)
{
    public bool IsPaid => Status == "paid";
    public bool IsPending => Status == "pending";
    public bool IsFailed => Status == "failed";
}
public sealed partial class DashboardViewModel : ObservableObject
{
    public const double ChartWidth = 600, ChartHeight = 180;

    private double[] _traffic = [];

    [ObservableProperty] private Kpi _revenueKpi = null!;
    [ObservableProperty] private Kpi _ordersKpi = null!;
    [ObservableProperty] private Kpi _usersKpi = null!;
    [ObservableProperty] private Kpi _conversionKpi = null!;
    [ObservableProperty] private Bar[] _bars = [];
    [ObservableProperty] private Order[] _recentOrders = [];
    [ObservableProperty] private string _trafficNow = "";
    [ObservableProperty] private string _updated = "";

    public IList<Point> TrafficLine => Line();
    public IList<Point> TrafficArea => [.. Line(), new Point(ChartWidth, ChartHeight), new Point(0, ChartHeight)];

    public DashboardViewModel(DashboardData data) => Receive(data);

    public void Receive(DashboardData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        RevenueKpi = data.Revenue;
        OrdersKpi = data.Orders;
        UsersKpi = data.Users;
        ConversionKpi = data.Conversion;
        _traffic = data.Traffic;
        OnPropertyChanged(nameof(TrafficLine));
        OnPropertyChanged(nameof(TrafficArea));
        TrafficNow = data.TrafficNow;
        Bars = data.Bars;
        RecentOrders = data.RecentOrders;
        Updated = data.Updated;
    }

    private List<Point> Line()
    {
        var line = new List<Point>(_traffic.Length);
        for (var i = 0; i < _traffic.Length; i++)
            line.Add(new Point(i * ChartWidth / Math.Max(1, _traffic.Length - 1), ChartHeight - _traffic[i] / 1000 * (ChartHeight - 10)));
        return line;
    }
}
