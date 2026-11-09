using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Native;

/// <summary>
/// Opens the system folder picker through Avalonia's <c>StorageProvider</c> and lists what the chosen folder holds:
/// something only the installed app can do, so its page is native-only and the web shows the stand-in.
/// </summary>
public sealed class FolderBrowser : UserControl
{
    private const int Shown = 200;

    private readonly TextBlock _folder = new() { Text = "No folder chosen yet.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ItemsControl _entries = new();

    public FolderBrowser()
    {
        _folder.Classes.Add("muted");
        Content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Button { Content = "Choose a folder", Command = new AsyncRelayCommand(ChooseAsync), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left },
                _folder,
                _entries,
            },
        };
    }

    private async Task ChooseAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage)
        {
            _folder.Text = "This platform cannot open a folder picker.";
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder", AllowMultiple = false });
        if (folders.Count == 0)
        {
            return;
        }

        var names = new List<string>();
        await foreach (var item in folders[0].GetItemsAsync())
        {
            names.Add(item is IStorageFolder ? item.Name + "/" : item.Name);
            if (names.Count == Shown)
            {
                break;
            }
        }

        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        _folder.Text = $"{folders[0].Path.LocalPath}: {names.Count} items{(names.Count == Shown ? " shown" : string.Empty)}.";
        _entries.ItemsSource = names;
    }
}
