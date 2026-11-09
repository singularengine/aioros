using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Levels : ObservableObject
{
    [ObservableProperty]
    public partial double Volume { get; set; } = 40;

    [ObservableProperty]
    public partial double Progress { get; set; } = 30;
}

public sealed partial class Schedule : ObservableObject
{
    [ObservableProperty]
    public partial DateTime? Day { get; set; } = new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    [ObservableProperty]
    public partial DateTimeOffset? Start { get; set; } = new DateTimeOffset(2024, 3, 14, 0, 0, 0, TimeSpan.FromHours(2));

    [ObservableProperty]
    public partial TimeSpan? At { get; set; } = new TimeSpan(13, 45, 0);
}

public sealed partial class Span : ObservableObject
{
    [ObservableProperty]
    public partial double Low { get; set; } = 200;

    [ObservableProperty]
    public partial double High { get; set; } = 800;
}
