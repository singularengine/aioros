using Aioros.Pages;

namespace Lumen.Data;

public sealed class IssueSitemap(Store store) : ISitemapSource
{
    public IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken) => store.Issues.Select(issue => issue.Href).ToAsyncEnumerable();
}
