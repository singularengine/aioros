using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed partial class RangesModel : ObservableObject
{
    [ObservableProperty]
    public partial double Volume { get; set; } = 40;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Corners), nameof(Lean))]
    public partial double Progress { get; set; } = 60;

    public Avalonia.CornerRadius Corners => new(Progress / 5);

    public double Lean => (Progress - 50) / 4;

    [ObservableProperty]
    public partial DateTime? Day { get; set; } = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    [ObservableProperty]
    public partial DateTimeOffset? Start { get; set; } = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);

    [ObservableProperty]
    public partial TimeSpan? At { get; set; } = new TimeSpan(13, 45, 0);

    [ObservableProperty]
    public partial string Summary { get; set; } = "Nothing saved yet.";

    [RelayCommand]
    private void Save()
    {
        Progress = Volume;
        Summary = string.Create(
            CultureInfo.InvariantCulture,
            $"Saved volume {Volume:0.#}, day {Day:yyyy-MM-dd}, start {Start:yyyy-MM-dd}, at {At:hh\\:mm}.");
    }
}
