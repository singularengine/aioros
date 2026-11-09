using Aioros.Navigation;
using Aioros.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class Home : IPageModel
{
    public string Title => "Home";
}

public sealed record ItemData(string Label);

public sealed class Item : IPageModel
{
    public Item()
    {
    }

    public Item(ItemData data) => (Label, _fromData) = (data.Label, true);

    private readonly bool _fromData;

    public static int Loads { get; set; }

    public string Label { get; private set; } = string.Empty;

    public string Title => "Item " + Label;

    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        Loads++;
        Label = _fromData ? Label : (request.Value("id") ?? string.Empty) + (request.Query.GetValueOrDefault("tab") is { } tab ? "/" + tab : string.Empty);
        return ValueTask.CompletedTask;
    }
}

public sealed partial class Form : ObservableObject, IPageModel
{
    public string? RedirectTo { get; private set; }

    [RelayCommand]
    private void Save() => RedirectTo = "/about";
}

public sealed class Gone : IPageModel
{
    public bool NotFound => true;
}

public sealed class Old : IPageModel
{
    public string? RedirectTo => "/about";
}

public sealed class Broken : IPageModel
{
    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("The board is down.");
}

public sealed partial class Leaving(INavigator navigator) : ObservableObject, IPageModel
{
    public static Leaving? Last { get; private set; }

    public INavigator Navigator { get; } = navigator;

    public TaskCompletionSource Away { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? RedirectTo { get; private set; }

    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        Last = this;
        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private Task GoAsync()
    {
        RedirectTo = "/items/1";
        return Navigator.NavigateAsync(new NavigationRequest("about"), CancellationToken.None).AsTask();
    }

    [RelayCommand]
    private async Task AwayAsync()
    {
        await Navigator.NavigateAsync(new NavigationRequest("https://example.com/docs"), CancellationToken.None);
        Away.TrySetResult();
    }
}

public sealed class Entering : IPageModel
{
    public Entering(INavigator navigator) => _ = navigator.NavigateAsync(new NavigationRequest("/about"), CancellationToken.None).AsTask();
}

public sealed class Arriving(INavigator navigator) : IPageModel
{
    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken) => navigator.NavigateAsync(new NavigationRequest("items/5"), cancellationToken);
}

public sealed class Lease : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed record SectionData(string Path);

public sealed class Section : IPageModel
{
    public Section()
    {
    }

    public Section(SectionData data) => (Path, _fromData) = (data.Path, true);

    private readonly bool _fromData;

    public string Path { get; private set; } = string.Empty;

    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        Path = _fromData ? Path : request.Path;
        return ValueTask.CompletedTask;
    }
}

public sealed partial class Pick : ObservableObject, IPageModel
{
    public static Pick? Last { get; private set; }

    public Pick() => Last = this;

    public int Count { get; } = 5;

    [ObservableProperty]
    public partial int Index { get; set; } = 2;
}

public interface IAsyncCommand : System.Windows.Input.ICommand
{
    Task ExecuteAsync(object? parameter);
}

public sealed class PrismShapedCommand(Func<Task> execute) : IAsyncCommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

#pragma warning disable VSTHRD100
    public async void Execute(object? parameter) => await execute();
#pragma warning restore VSTHRD100

    Task IAsyncCommand.ExecuteAsync(object? parameter) => execute();
}

public sealed class Later : IPageModel
{
    public Later()
    {
        Last = this;
        SendCommand = new PrismShapedCommand(SendAsync);
        BreakCommand = new PrismShapedCommand(static () => Task.FromException(new InvalidOperationException("The save failed.")));
    }

    public static Later? Last { get; private set; }

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Sends { get; private set; }

    public string? RedirectTo { get; private set; }

    public IAsyncCommand SendCommand { get; }

    public IAsyncCommand BreakCommand { get; }

    private async Task SendAsync()
    {
        Sends++;
        await Gate.Task;
        RedirectTo = "/about";
    }
}

public sealed class Detour
{
    public static int Created { get; set; }

    public int Order { get; } = ++Created;
}

public sealed class Exclusive : IAsyncDisposable
{
    private int _operations;

    public bool Disposed { get; private set; }

    public async Task<T> UseAsync<T>(Func<Task<T>> operation)
    {
        if (Interlocked.Increment(ref _operations) > 1)
        {
            Interlocked.Decrement(ref _operations);
            throw new InvalidOperationException("A second operation was started on this context instance before a previous operation completed.");
        }

        try
        {
            return await operation();
        }
        finally
        {
            Interlocked.Decrement(ref _operations);
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed partial class Saving(ItemData data, INavigator? navigator) : ObservableObject, System.ComponentModel.INotifyDataErrorInfo
{
    public static Saving? Last { get; private set; }

    public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

    [ObservableProperty]
    public partial string Label { get; set; } = data.Label;

    [ObservableProperty]
    public partial string Saved { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public bool HasErrors => Error is not null;

    public System.Collections.IEnumerable GetErrors(string? propertyName) => Error is null ? Array.Empty<string>() : new[] { Error };

    public void Receive(ItemData data) => Label = data.Label;

    public Saving Remember() => Last = this;

    /// <summary>What the save changes besides its own state: a server function call, which records the keys it invalidated.</summary>
    public Func<Task>? Change { get; init; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Saved = "Saved";
        if (Change is not null)
        {
            await Change();
        }
    }

    [RelayCommand]
    private static void Break() => throw new InvalidOperationException("The save failed.");

    [RelayCommand]
    private void Reject()
    {
        Error = "A label is required.";
        ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(nameof(Label)));
    }

    [RelayCommand]
    private Task LeaveAsync() => navigator!.NavigateAsync(new NavigationRequest("/about"), CancellationToken.None).AsTask();
}

public sealed record TeamData(string Name);

public sealed partial class Team(TeamData data) : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = data.Name;

    public void Receive(TeamData data) => Name = data.Name;
}

public sealed record Card(string Id);

public sealed record Deck(string Label, Card[] Cards);

public sealed record ShelfData(IReadOnlyList<string> Books);

public sealed partial class Shelving(ShelfData data, Func<(string Item, string Target, int Index), Task> move) : ObservableObject
{
    [ObservableProperty]
    public partial IReadOnlyList<string> Books { get; set; } = data.Books;

    public void Receive(ShelfData data) => Books = data.Books;

    [RelayCommand]
    private Task MoveAsync((string Item, string Target, int Index) placed) => move(placed);
}

public sealed class Reloaded
{
    public string Name { get; set; } = "Ada";
}
