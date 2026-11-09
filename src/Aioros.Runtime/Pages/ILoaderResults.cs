using System.Collections.Immutable;
using Aioros.Pages;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Where the loader results of a page come from when its loaders run elsewhere: the installed app asks the server for them
/// in one request (<c>/_data{route}</c>) instead of running them in process.
/// </summary>
public interface ILoaderResults
{
    /// <summary>The results of the page's layouts' and its own loaders, outermost first, one per segment.</summary>
    ValueTask<LoaderResults> LoadAsync(PageDefinition page, PageRequest request, CancellationToken cancellationToken);

    /// <summary>The result of one view instance's loader alone (by its index in <see cref="PageDefinition.Views"/>), after a command in that view.</summary>
    ValueTask<ViewResult> LoadViewAsync(PageDefinition page, PageRequest request, int view, CancellationToken cancellationToken);

    /// <summary>The result of one item of a repeated view instance's loader alone, after a command in that item, as <see cref="ItemResults"/>.</summary>
    ValueTask<ViewResult> LoadItemAsync(PageDefinition page, PageRequest request, int view, string item, CancellationToken cancellationToken);
}

/// <summary>The loader results of a page's segments, outermost first, and what they answer together.</summary>
public readonly record struct PageResults(ImmutableArray<LoaderOutcome> Segments, PageOutcome Outcome)
{
    /// <summary>The data keys each slot's loader read, by slot; empty when none read any.</summary>
    public ImmutableArray<ImmutableArray<string>> Keys { get; init; }
}
