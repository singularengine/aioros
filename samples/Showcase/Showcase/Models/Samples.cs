using Aioros.Pages;

namespace Showcase.Models;

public sealed class Samples : IPrecompileSource
{
    public IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken) => new[] { "/crm/1", "/games/1", "/ports/kartova" }.ToAsyncEnumerable();
}
