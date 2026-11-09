using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Diary : ObservableObject
{
    public DateTime When { get; } = new(2025, 6, 12, 0, 0, 0, DateTimeKind.Unspecified);

    public double Amount { get; } = 1234.5;

    [ObservableProperty]
    public partial string? Noted { get; set; }

    [RelayCommand]
    private void Note() => Noted = CultureInfo.CurrentCulture.Name + " " + When.ToString("D", CultureInfo.CurrentCulture);
}
