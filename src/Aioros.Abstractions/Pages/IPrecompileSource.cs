namespace Aioros.Pages;

/// <summary>
/// Sample URLs of routes with parameters, used only to compile their plans ahead of the first visit (like Next.js
/// <c>generateStaticParams</c>); unlike <see cref="ISitemapSource"/>, they are not published.
/// </summary>
public interface IPrecompileSource
{
    IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken);
}
