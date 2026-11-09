using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Upload : ObservableObject
{
    public static FilePickerFileType Pdf { get; } = new("PDF") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] };

    public static FilePickerFileType Everything { get; } = new("Everything") { Patterns = ["*.*"] };

    [ObservableProperty]
    public partial IStorageFile? Photo { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<IStorageFile>? Documents { get; set; }

    public string? Read { get; private set; }

    [RelayCommand]
    private async Task ReadAsync()
    {
        await using var stream = await Photo!.OpenReadAsync();
        using var reader = new StreamReader(stream);
        Read = await reader.ReadToEndAsync();
    }
}
