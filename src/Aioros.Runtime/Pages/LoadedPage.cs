using System.Collections.Immutable;
using Aioros.Pages;

namespace Aioros.Runtime.Pages;

/// <summary>
/// A page once loaded: the models of its layouts and the page, outermost first, then its views', and what its loaders answered besides
/// their data, when it has loaders.
/// </summary>
public readonly record struct LoadedPage(ImmutableArray<object?> Models, PageOutcome? Outcome = null)
{
    /// <summary>How many of the models, at the end, are the page's views' (or their <see cref="Rendering.ViewState"/> markers).</summary>
    public int Views { get; init; }

    /// <summary>
    /// The data keys each slot's loader read in its last load, by slot; default when no loader read any, so a page without
    /// keys carries nothing for them.
    /// </summary>
    public ImmutableArray<ImmutableArray<string>> Keys { get; init; }
}

/// <summary>What the loaders of a page and its layouts answered together, merged from the outermost to the page.</summary>
public readonly record struct PageOutcome(bool NotFound, string? RedirectTo, string? Title, PageMetadata? Metadata)
{
    /// <summary>Whether a loader stopped the page, answering not found or a redirect, so no view model was created.</summary>
    public bool Stops => NotFound || RedirectTo is not null;

    /// <summary>The not-found page's results that came with a remote not-found answer, when the server sent them.</summary>
    public RemoteNotFound? NotFoundPage { get; init; }
}

/// <summary>What a page answers instead of rendering: not found, a redirect, or neither when it renders.</summary>
public readonly record struct PageAnswer(bool NotFound, string? RedirectTo)
{
    public bool Renders => !NotFound && RedirectTo is null;
}
