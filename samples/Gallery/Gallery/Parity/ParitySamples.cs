using Aioros.Pages;

namespace Gallery.Parity;

public sealed class ParitySamples : IPrecompileSource
{
    public IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken) => new[] { "/parity/text" }.ToAsyncEnumerable();
}
