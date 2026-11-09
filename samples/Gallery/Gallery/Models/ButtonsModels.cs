using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gallery.Models;

public sealed partial class ButtonsModel : ObservableObject
{
    [ObservableProperty]
    public partial Avalonia.Platform.Storage.IStorageFile? Attachment { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Avalonia.Platform.Storage.IStorageFile>? Attachments { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = "Not clicked yet.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Wide), nameof(Fade), nameof(Inset))]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool Bold { get; set; } = true;

    [ObservableProperty]
    public partial bool Italic { get; set; }

    [ObservableProperty]
    public partial bool Small { get; set; }

    [ObservableProperty]
    public partial bool Medium { get; set; } = true;

    [ObservableProperty]
    public partial bool Large { get; set; }

    [ObservableProperty]
    public partial bool Agree { get; set; } = true;

    [ObservableProperty]
    public partial bool? All { get; set; }

    [ObservableProperty]
    public partial bool WiFi { get; set; } = true;

    [ObservableProperty]
    public partial bool Bluetooth { get; set; }

    public double Wide => 120 + (Math.Min(Count, 10) * 8);

    public double Fade => Count % 2 == 0 ? 1 : 0.6;

    public Avalonia.Thickness Inset => new(12 + Math.Min(Count, 6) * 2, 6);

    public Avalonia.Media.IBrush Paint { get; } = Avalonia.Media.Brush.Parse("#0f766e");

    public Avalonia.Media.IBrush Ink { get; } = Avalonia.Media.Brushes.White;

    [RelayCommand]
    private void Click() => Message = "Clicked on the server.";

    [RelayCommand]
    private void Repeat() => Count++;

    [RelayCommand]
    private void Send() => Message = "Sent from the split button.";
}
