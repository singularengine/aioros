using Avalonia.Platform.Storage;

namespace Showcase.Uploads;

/// <summary>
/// What only one host can say about a chosen file, behind one interface registered per host (MAUI's <c>IFormFactor</c>):
/// the desktop knows the real file on disk, the web knows it holds an upload for the length of the post.
/// </summary>
public interface IFileOrigin
{
    string Describe(IStorageFile file);
}

/// <summary>The desktop's: the file the system picker chose, at its real path.</summary>
public sealed class LocalFileOrigin : IFileOrigin
{
    public string Describe(IStorageFile file) =>
        file.TryGetLocalPath() is { } path ? $"Read from {path} on this computer; the app could open it again or watch it for changes." : "Read through the system picker.";
}

/// <summary>The website's: an upload the server reads while it answers the post.</summary>
public sealed class UploadedFileOrigin(long limit) : IFileOrigin
{
    public string Describe(IStorageFile file) =>
        $"Uploaded by the browser with the form: the server read a temporary copy of {file.Name}, kept only while it answered, up to {limit / (1024 * 1024)} MB per file.";
}
