using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Kartova.App.ViewModels;

namespace Kartova.App.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // The window owns the platform services the view model needs but should not know about.
        DataContextChanged += (_, _) => WireUpViewModel();
        AttachedToVisualTree += (_, _) => OnOpened();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WireUpViewModel()
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.PickFoldersAsync = PickFoldersAsync;
        viewModel.PickSaveFileAsync = PickSaveFileAsync;
        viewModel.CopyToClipboardAsync = CopyToClipboardAsync;
        OnOpened();
    }

    private Window? Window => TopLevel.GetTopLevel(this) as Window;

    private void OnOpened()
    {
        if (_opened || Window is not { } window || DataContext is not MainViewModel viewModel) return;

        _opened = true;
        window.Closing += OnClosing;
        var settings = viewModel.Settings;
        if (settings.WindowWidth > 400 && settings.WindowHeight > 300)
        {
            window.Width = settings.WindowWidth;
            window.Height = settings.WindowHeight;
        }

        if (settings.WindowMaximized) window.WindowState = WindowState.Maximized;

        // Command line: "Kartova C:\Some\Folder" scans immediately.
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Args: { Length: > 0 } args })
            viewModel.ScanFromCommandLine(args);
    }

    private bool _opened;

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        viewModel.PersistWindowState(Window!.Width, Window.Height, Window.WindowState == WindowState.Maximized);
        viewModel.PersistSettings();
    }

    // ------------------------------------------------------- platform services

    private async Task<IReadOnlyList<string>> PickFoldersAsync()
    {
        var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folders to scan",
            AllowMultiple = true,
        });

        return folders
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToArray();
    }

    private async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var extension = Path.GetExtension(suggestedName).TrimStart('.');

        var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export scan",
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices =
            [
                extension == "csv"
                    ? new FilePickerFileType("CSV") { Patterns = ["*.csv"] }
                    : new FilePickerFileType("JSON") { Patterns = ["*.json"] },
            ],
        });

        return file?.TryGetLocalPath();
    }

    private async Task CopyToClipboardAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    // ---------------------------------------------------------- about dialog

    /// <summary>Dismisses the About dialog when the dimmed backdrop is clicked.</summary>
    private void OnAboutScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.IsAboutOpen = false;
    }

    /// <summary>
    /// Escape closes the About dialog.
    /// </summary>
    /// <remarks>
    /// Registered as a tunnelling handler so it runs before the screens underneath see the key.
    /// The dialog covers them, so a keystroke aimed at it should never reach the view behind.
    /// </remarks>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not MainViewModel { IsAboutOpen: true } viewModel) return;

        viewModel.IsAboutOpen = false;
        e.Handled = true;
    }
}
