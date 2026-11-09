namespace Aioros.Pages;

/// <summary>
/// Deprecated: return the title, the metadata, not found and redirects from the page's loader
/// (<see cref="LoaderResult{TData}"/>) instead. Still read as a fallback for what no loader gives.
/// </summary>
public interface IPageModel
{
    string Title => string.Empty;

    PageMetadata Metadata => PageMetadata.None;

    /// <summary>
    /// Deprecated: inject <see cref="Navigation.INavigator"/> and navigate from the command, or return
    /// <see cref="LoaderResult.Redirect"/> from the loader instead. Still answered when no navigation was asked of the
    /// navigator and no loader redirected, which win when set.
    /// </summary>
    string? RedirectTo => null;

    bool NotFound => false;

    ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
