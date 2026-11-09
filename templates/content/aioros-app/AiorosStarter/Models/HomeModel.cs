using CommunityToolkit.Mvvm.Input;

namespace AiorosStarter.Models;

public sealed record Clicks(int Count);

public sealed partial class HomeModel(Clicks clicks, ClickCounter counter)
{
    public string Message { get; } = $"Clicked {clicks.Count} times.";

    [RelayCommand]
    private void Count() => counter.Increment();
}
