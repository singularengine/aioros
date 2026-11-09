using AsyncImageLoader.Core.Pipeline;
using AsyncImageLoader.Core.Sources;
using Aioros.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Gallery.Parity;

/// <summary>On the desktop, where the app has no web server, AsyncImageLoader reads the report's captures from the web root.</summary>
public sealed class ParityImages(IFileProvider webRoot) : IImageSourceResolver
{
    public Task<ResolvedImageSource?> ResolveAsync(ImageLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = request.Source.StartsWith($"/{ParityFile.Folder}/", StringComparison.Ordinal) ? webRoot.GetFileInfo(request.Source.TrimStart('/')) : null;
        return Task.FromResult(file is { Exists: true } ? new ResolvedImageSource(file.CreateReadStream()) : null);
    }
}

public static class ParitySetup
{
    /// <summary>Shows the report's captures on the desktop; the web serves them as static files.</summary>
    public static WebApplication ShowParityCaptures(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Services.GetRequiredService<AiorosMode>().Desktop)
        {
            AsyncImageLoader.ImageLoader.AsyncImageLoader = ImageLoaderPipelineBuilder.RamCached().UseSourceResolver(new ParityImages(app.Environment.WebRootFileProvider)).Build();
        }

        return app;
    }
}
