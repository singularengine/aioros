namespace Aioros.Pages;

public interface ISitemapSource
{
    IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken);
}
