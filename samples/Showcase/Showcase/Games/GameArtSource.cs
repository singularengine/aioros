using AsyncImageLoader.Core.Pipeline;
using AsyncImageLoader.Core.Sources;

namespace Showcase.Games;

/// <summary>On the desktop, where the app has no web server, AsyncImageLoader gets the game art straight from <see cref="GameArt"/>.</summary>
public sealed class GameArtSource : IImageSourceResolver
{
    public Task<ResolvedImageSource?> ResolveAsync(ImageLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(GameArt.TryParse(request.Source, out var id, out var kind)
            ? new ResolvedImageSource(new MemoryStream(GameArt.Render(id, kind)))
            : null);
    }
}
