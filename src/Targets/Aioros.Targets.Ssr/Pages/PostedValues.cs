using System.Collections.Immutable;
using System.Globalization;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// A post's form: its values, and the files it carries copied into a temporary folder of their own, each under the name
/// it was uploaded with, which goes when the post is answered. ASP.NET Core's form reader keeps every part above 64 KB
/// in a temporary file, so no upload is held in memory, and refuses a part above the limit (<c>MultipartBodyLengthLimit</c>).
/// A notice's hidden field becomes the prompt field when one of its buttons was pressed (<see cref="PlanForms.Overlays"/>), and a
/// range's two values come in order (<see cref="PlanForms.Ranges"/>).
/// </summary>
internal sealed class PostedValues(ImmutableDictionary<string, string> values, ImmutableArray<PostedFile> files, string? folder) : IDisposable
{
    private const string Unnamed = "upload";

    public ImmutableDictionary<string, string> Values { get; } = values;

    public ImmutableArray<PostedFile> Files { get; } = files;

    /// <summary>The post's form, or nothing when a file is larger than the limit.</summary>
    public static async Task<PostedValues?> ReadAsync(HttpContext http, long limit, CancellationToken cancellationToken)
    {
        if (http.Request.HasFormContentType && http.Features.Get<IFormFeature>() is not { Form: not null })
        {
            http.Features.Set<IFormFeature>(new FormFeature(http.Request, new FormOptions { MultipartBodyLengthLimit = limit }));
        }

        IFormCollection form;
        try
        {
            form = await http.Request.ReadFormAsync(cancellationToken);
        }
        catch (Exception error) when (error is InvalidDataException or BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            return null;
        }

        var values = PlanForms.Ranges(PlanForms.Overlays(form.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal)));
        var chosen = form.Files;
        if (chosen.Count == 0)
        {
            return new PostedValues(values, [], null);
        }

        var folder = Directory.CreateTempSubdirectory("aioros-upload-").FullName;
        var files = ImmutableArray.CreateBuilder<PostedFile>(chosen.Count);
        for (var index = 0; index < chosen.Count; index++)
        {
            files.Add(await CopyAsync(chosen[index], Path.Combine(folder, index.ToString(CultureInfo.InvariantCulture)), cancellationToken));
        }

        return new PostedValues(values, files.MoveToImmutable(), folder);
    }

    public void Dispose()
    {
        if (folder is not null && Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<PostedFile> CopyAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Name(file.FileName));
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await using var source = file.OpenReadStream();
            await source.CopyToAsync(target, cancellationToken);
        }

        return new PostedFile(file.Name, Path.GetFileName(path), string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType, file.Length, path);
    }

    /// <summary>The uploaded name without any folder the browser sent, and with what a file name cannot hold replaced.</summary>
    private static string Name(string uploaded)
    {
        var name = string.Concat(Path.GetFileName(uploaded.Replace('\\', '/')).Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return name is "" or "." or ".." ? Unnamed : name;
    }
}
