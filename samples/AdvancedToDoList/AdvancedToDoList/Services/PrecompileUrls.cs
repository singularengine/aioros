using Aioros.Pages;

namespace AdvancedToDoList.Services;

/// <summary>One address of each page with route values, so <c>aioros build</c> precompiles them too.</summary>
public sealed class PrecompileUrls : IPrecompileSource
{
    public IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken) =>
        new[] { "/items/1", "/categories/1", "/confirm/delete-item/1" }.ToAsyncEnumerable();
}
