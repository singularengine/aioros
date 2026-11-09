using AsyncImageLoader.Core.Pipeline;
using AsyncImageLoader.Core.Sources;
using Aioros.Hosting;
using Microsoft.Extensions.FileProviders;
using Showcase.Games;
using Showcase.Ports;

namespace Showcase.Images;

/// <summary>On the desktop, where the app has no web server, AsyncImageLoader reads the ported apps' captures from the web root.</summary>
public sealed class PortCaptures(IFileProvider webRoot) : IImageSourceResolver
{
    public Task<ResolvedImageSource?> ResolveAsync(ImageLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = request.Source.StartsWith($"/{PortCatalog.Captures}/", StringComparison.Ordinal) ? webRoot.GetFileInfo(request.Source.TrimStart('/')) : null;
        return Task.FromResult(file is { Exists: true } ? new ResolvedImageSource(file.CreateReadStream()) : null);
    }
}

public static class DesktopImages
{
    /// <summary>Shows the game art and the ported apps' captures on the desktop; the web serves them over HTTP.</summary>
    public static WebApplication ShowImagesOnDesktop(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Services.GetRequiredService<AiorosMode>().Desktop)
        {
            AsyncImageLoader.ImageLoader.AsyncImageLoader = ImageLoaderPipelineBuilder.RamCached()
                .UseSourceResolver(new CompositeImageSourceResolver(new GameArtSource(), new PortCaptures(app.Environment.WebRootFileProvider)))
                .Build();
        }

        return app;
    }
}
