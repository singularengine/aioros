using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A streamed turn as Aioros.Cosmo's Gallery plays one back: its question shown once asked, its line's status, and Replay enabled while it is idle.</summary>
public sealed partial class Turn : ObservableObject
{
    [ObservableProperty]
    public partial bool Asked { get; set; }

    [ObservableProperty]
    public partial bool Idle { get; set; }

    [ObservableProperty]
    public partial PhaseStatus Phase { get; set; }

    [RelayCommand]
    private void Replay() => Idle = false;
}
