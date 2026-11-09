using System.Security.Cryptography;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Uploads;

/// <summary>
/// Reads files the same way on every host: the pickers fill Avalonia's own <see cref="IStorageFile"/> properties, from
/// the system picker on the desktop and from the post on the web, and only <see cref="IFileOrigin"/> differs per host.
/// </summary>
public sealed partial class UploadsModel(IFileOrigin origin) : ObservableObject
{
    [ObservableProperty]
    public partial IStorageFile? Photo { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<IStorageFile>? Documents { get; set; }

    [ObservableProperty]
    public partial string Report { get; set; } = "Nothing read yet.";

    [ObservableProperty]
    public partial string Origin { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<string> Listed { get; set; } = [];

    [RelayCommand]
    private async Task ReadPhotoAsync()
    {
        if (Photo is not { } photo)
        {
            Report = "Choose a photo first.";
            return;
        }

        var size = (await photo.GetBasicPropertiesAsync()).Size ?? 0;
        await using var stream = await photo.OpenReadAsync();
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream))[..16];
        Report = $"{photo.Name}: {size:N0} bytes, SHA-256 {hash}…";
        Origin = origin.Describe(photo);
    }

    [RelayCommand]
    private async Task ListDocumentsAsync()
    {
        var listed = new List<string>();
        foreach (var document in Documents ?? [])
        {
            listed.Add($"{document.Name} · {(await document.GetBasicPropertiesAsync()).Size ?? 0:N0} bytes");
        }

        Listed = listed.Count == 0 ? ["Choose one or more documents first."] : listed;
    }
}
