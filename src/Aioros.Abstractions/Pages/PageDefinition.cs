using System.Collections.Immutable;

namespace Aioros.Pages;

public sealed record PageDefinition(
    string Route,
    string Source,
    Func<IServiceProvider, object?> CreateModel,
    ImmutableArray<LayoutDefinition> Layouts,
    bool Live = false)
{
    public ImmutableArray<string> Sources => [.. Layouts.Select(layout => layout.Source), Source];

    /// <summary>The <c>[Authorize]</c> rules of the layouts' and the page's loaders (and, deprecated, view models), outermost first; every one must hold.</summary>
    public ImmutableArray<PageAccess> Access { get; init; } = [];

    /// <summary>The page's loader (or, deprecated, its view model) has <c>[AllowAnonymous]</c>: no rule applies to it, folder conventions included.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>The page's loader, when it has one; its view model is then created from the loader's data.</summary>
    public LoaderDefinition? Loader { get; init; }

    /// <summary>The page's static title and metadata, declared in its XAML.</summary>
    public PageHead? Head { get; init; }

    /// <summary>Where the page runs, declared in its XAML (<c>a:Page.Platforms</c>); every platform unless set.</summary>
    public PagePlatforms Platforms { get; init; } = PagePlatforms.All;

    /// <summary>The page leaves out the web: there its route answers with the native-only stand-in.</summary>
    public bool NativeOnly => (Platforms & PagePlatforms.Web) == 0;

    /// <summary>
    /// The hash of the data shapes of the layouts' and the page's loaders, outermost first: the installed app sends it with
    /// every data request and the server answers <c>426</c> when its own differs.
    /// </summary>
    public string DataContract { get; init; } = string.Empty;

    /// <summary>
    /// The views placed in the layouts and the page, nested ones included, in document order: their view models take the
    /// slots after the page's, so a page's models are its layouts', its own and then one per instance.
    /// </summary>
    public ImmutableArray<ViewInstance> Views { get; init; } = [];

    /// <summary>
    /// A model of the page (its layouts', its own, its views' or their loaders') creates an Avalonia object as it is
    /// constructed, so the page's models are created on Avalonia's thread, which starts for them: an Avalonia object
    /// belongs to the thread that creates it, and the first one created takes Avalonia's UI dispatcher for its thread.
    /// </summary>
    public bool ModelsUseAvalonia { get; init; }

    /// <summary>
    /// The binding paths, as the page's XAML writes them, of the commands the generator found run on any thread: they touch
    /// no Avalonia object, dispatcher or platform and call no code that may, so a post pressing one is run through the
    /// page's plan even while Avalonia runs in the process; any other command runs on Avalonia's thread.
    /// </summary>
    public ImmutableArray<string> AnywhereCommands { get; init; } = [];

    /// <summary>The source of the app's view error template (<c>Pages/ViewError.axaml</c>), shown where a view's loader failed.</summary>
    public string? ViewError { get; init; }

    /// <summary>The slot of the first view's model: after the layouts' and the page's.</summary>
    public int FirstView => Layouts.Length + 1;

    /// <summary>How many models the page renders with: its layouts', its own and its views'.</summary>
    public int ModelCount => Layouts.Length + 1 + Views.Length;
}

/// <summary>One <c>[Authorize]</c> rule: signed in, and the policy and roles when they are set.</summary>
public sealed record PageAccess(string? Policy, string? Roles);

public sealed record LayoutDefinition(string Source, Func<IServiceProvider, object?> CreateModel)
{
    /// <summary>The layout's loader, when it has one; its view model is then created from the loader's data.</summary>
    public LoaderDefinition? Loader { get; init; }

    /// <summary>The layout's static title, title template and metadata, declared in its XAML.</summary>
    public PageHead? Head { get; init; }
}
