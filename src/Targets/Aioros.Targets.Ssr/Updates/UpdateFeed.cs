using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Aioros.Targets.Ssr.Updates;

/// <summary>
/// The installed app's update feed on the website: <c>/updates/</c> serves the Velopack release folder (the release index,
/// full and delta packages, the AppImage, the Windows installer, the portable zip) through ASP.NET Core's static files,
/// so ranges, ETags and <c>304</c> come with them. Packages carry their version in their name and never change, so they
/// are cached for a year; everything else names the latest release and is revalidated on every check (<c>no-cache</c>).
/// Files Velopack does not write are not served.
/// </summary>
public static class UpdateFeed
{
    /// <summary>The configuration key of the folder <c>aioros build --installer</c> packed into (<c>.aioros/installer/releases</c>), copied to the server.</summary>
    public const string FolderSetting = "Aioros:UpdatesFolder";

    /// <summary>Where the feed is served, the installed app's default feed on its server.</summary>
    public const string Path = "/updates";

    private const string Immutable = "public, max-age=31536000, immutable";

    /// <summary>Serves <paramref name="folder"/> under <c>/updates/</c>; nothing when it is not set.</summary>
    public static IApplicationBuilder UseUpdateFeed(this IApplicationBuilder app, string? folder)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (string.IsNullOrEmpty(folder))
        {
            return app;
        }

        return app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = Path,
            FileProvider = new PhysicalFileProvider(System.IO.Path.GetFullPath(folder)),
            ContentTypeProvider = new FeedTypes(),
            OnPrepareResponse = static context => context.Context.Response.Headers.CacheControl =
                context.File.Name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) ? Immutable : "no-cache",
        });
    }

    /// <summary>The media types of the files Velopack writes; any other file is not part of the feed.</summary>
    private sealed class FeedTypes : IContentTypeProvider
    {
        public bool TryGetContentType(string subpath, [MaybeNullWhen(false)] out string contentType)
        {
            var name = System.IO.Path.GetFileName(subpath);
            contentType = name == "RELEASES" || name.StartsWith("RELEASES-", StringComparison.Ordinal) ? "text/plain" : System.IO.Path.GetExtension(name).ToUpperInvariant() switch
            {
                ".JSON" => "application/json",
                ".NUPKG" => "application/octet-stream",
                ".APPIMAGE" => "application/vnd.appimage",
                ".EXE" => "application/vnd.microsoft.portable-executable",
                ".MSI" => "application/x-msi",
                ".PKG" => "application/octet-stream",
                ".ZIP" => "application/zip",
                _ => null,
            };
            return contentType is not null;
        }
    }
}
