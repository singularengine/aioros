using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Runtime.Pages;

public static class PageLoader
{
    /// <summary>
    /// Creates the models of the page's layouts and the page, outermost first. The loaders of the page and its layouts are
    /// started together and awaited as one, and each model with a loader is created from its data once all have
    /// completed; a model without a loader comes from its factory as before. When a loader navigates, no model is created.
    /// </summary>
    public static ValueTask<LoadedPage> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, CancellationToken cancellationToken) =>
        LoadAsync(services, page, request, default, cancellationToken);

    /// <summary>
    /// Loads the page as <see cref="LoadAsync(IServiceProvider, PageDefinition, PageRequest, CancellationToken)"/> does,
    /// keeping the given models (by index, outermost first) that take new data: they receive their loader's fresh data
    /// instead of being created again.
    /// </summary>
    public static ValueTask<LoadedPage> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, ImmutableArray<object?> kept, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        var navigator = Navigator(services);
        navigator?.Open(request.Path);
        if (HasLoaders(page))
        {
            return LoadWithLoadersAsync(services, page, request, navigator, kept, cancellationToken);
        }

        var models = new object?[page.ModelCount];
        Create(services, page, null, models);
        var outcome = HasHeads(page) ? Merge([], page, request.Path) : (PageOutcome?)null;
        var reported = ModelLifecycles.ReportAsync(services, models, default, request, cancellationToken);
        return reported.IsCompletedSuccessfully
            ? LoadModelsAsync(models, 0, request, outcome, page.Views.Length, cancellationToken)
            : ReportedAsync(reported, models, request, outcome, page.Views.Length, cancellationToken);
    }

    private static async ValueTask<LoadedPage> ReportedAsync(ValueTask reported, object?[] models, PageRequest request, PageOutcome? outcome, int views, CancellationToken cancellationToken)
    {
        await reported;
        return await LoadModelsAsync(models, 0, request, outcome, views, cancellationToken);
    }

    private static bool HasHeads(PageDefinition page) => page.Head is not null || page.Layouts.Any(static layout => layout.Head is not null);

    private static async ValueTask<LoadedPage> LoadWithLoadersAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageNavigator? navigator, ImmutableArray<object?> kept, CancellationToken cancellationToken)
    {
        var load = PageLoad.For(services);
        var (results, keys) = await LoaderRuns.LoadAsync(services, page, request, load, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await FromResultsAsync(services, page, request, navigator, results, keys, kept, load, cancellationToken);
    }

    private static async ValueTask<LoadedPage> FromResultsAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageNavigator? navigator, LoaderOutcome[] results, ImmutableArray<string>[]? keys, ImmutableArray<object?> kept, PageLoad? load, CancellationToken cancellationToken)
    {
        var outcome = Merge(results, page, request.Path);
        var models = new object?[page.ModelCount];
        if (outcome.Stops || navigator?.Requested is not null)
        {
            return new LoadedPage(ImmutableCollectionsMarshal.AsImmutableArray(models), outcome) { Views = page.Views.Length };
        }

        var padded = Padded(results, page);
        Create(services, page, padded, models, kept);
        keys = await ItemsAsync(services, page, request, load, models, padded, keys, default, default, cancellationToken);
        await ModelLifecycles.ReportAsync(services, models, kept, request, cancellationToken);
        var loaded = await LoadModelsAsync(models, 0, request, outcome, page.Views.Length, cancellationToken);
        return keys is null ? loaded : loaded with { Keys = PageKeys.After(keys, default, page.ModelCount) };
    }

    /// <summary>
    /// Loads the not-found page shown instead of a page that answered not found: from the not-found page's results that
    /// came with the server's answer (<see cref="PageOutcome.NotFoundPage"/>), without asking for them again, and
    /// otherwise as <see cref="LoadAsync(IServiceProvider, PageDefinition, PageRequest, CancellationToken)"/>.
    /// </summary>
    public static ValueTask<LoadedPage> LoadNotFoundAsync(IServiceProvider services, PageDefinition notFound, PageRequest request, LoadedPage stopped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(notFound);
        ArgumentNullException.ThrowIfNull(request);
        if (stopped.Outcome?.NotFoundPage is not { } answer || !HasLoaders(notFound))
        {
            return LoadAsync(services, notFound, request, cancellationToken);
        }

        var navigator = Navigator(services);
        navigator?.Open(request.Path);
        return FromResultsAsync(services, notFound, request, navigator, ImmutableCollectionsMarshal.AsArray(answer.Read(notFound))!, null, default, null, cancellationToken);
    }

    /// <summary>
    /// Runs the loaders of the page and its layouts again after a command succeeded, each in a new scope, and returns the
    /// models holding the fresh data.
    /// </summary>
    public static ValueTask<LoadedPage> RevalidateAsync(IServiceProvider services, PageDefinition page, PageRequest request, LoadedPage current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        return HasLoaders(page) ? RevalidateWithLoadersAsync(services, page, request, current, default, cancellationToken) : new ValueTask<LoadedPage>(current);
    }

    /// <summary>
    /// Runs again the loaders of the given slots after a command in a view succeeded (<see cref="PageKeys.Invalidated"/>):
    /// only those views' loaders when no slot is the page's or a layout's, otherwise the page's and its layouts' loaders,
    /// which answer together (one title, one redirect), with those views'. The other views keep their models.
    /// </summary>
    public static ValueTask<LoadedPage> RevalidateSlotsAsync(IServiceProvider services, PageDefinition page, PageRequest request, LoadedPage current, ImmutableArray<int> slots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        return PageKeys.ReachesPage(page, slots)
            ? RevalidateWithLoadersAsync(services, page, request, current, slots, cancellationToken)
            : RevalidateViewsAsync(services, page, request, current, slots, cancellationToken);
    }

    /// <summary>
    /// Whether a command that completed should revalidate the page: it has loaders and no model reports errors through
    /// <see cref="INotifyDataErrorInfo.HasErrors"/>, as React Router revalidates nothing after a 4xx or 5xx action.
    /// </summary>
    public static bool Revalidates(PageDefinition page, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(page);
        return HasLoaders(page) && !models.IsDefault && models.All(static model => model is not INotifyDataErrorInfo { HasErrors: true });
    }

    private static bool HasLoaders(PageDefinition page) =>
        page.Loader is not null || page.Layouts.Any(static layout => layout.Loader is not null) || page.Views.Any(static view => view.View.Loader is not null);

    /// <summary>
    /// Loads the repeated views that run (every one by default, otherwise those given) from their hosts' models, and sets
    /// each one's item models, the kept ones receiving their fresh data.
    /// </summary>
    private static async ValueTask<ImmutableArray<string>[]?> ItemsAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageLoad? load, object?[] models, LoaderOutcome[] results, ImmutableArray<string>[]? keys, ImmutableArray<int> only, ImmutableArray<object?> kept, CancellationToken cancellationToken)
    {
        if (!page.Views.Any(static view => view.List is not null))
        {
            return keys;
        }

        keys = await ItemLoads.LoadAsync(services, page, request, load ?? PageLoad.For(services), models, results, keys, only, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        for (var slot = page.FirstView; slot < page.ModelCount; slot++)
        {
            if (Repeated(page, slot) && LoaderAt(page, slot) is not null && (only.IsDefault || only.Contains(slot)))
            {
                models[slot] = ItemLoads.Models(services, page, slot, results[slot], !kept.IsDefault && slot < kept.Length ? kept[slot] : null);
            }
        }

        return keys;
    }

    /// <summary>Whether a slot is a repeated view's, whose model is one per item of its list.</summary>
    public static bool Repeated(PageDefinition page, int index)
    {
        ArgumentNullException.ThrowIfNull(page);
        return index >= page.FirstView && index < page.ModelCount && page.Views[index - page.FirstView].List is not null;
    }

    /// <summary>The loader of a model's slot: a layout's, the page's or a view's.</summary>
    public static LoaderDefinition? LoaderAt(PageDefinition page, int index)
    {
        ArgumentNullException.ThrowIfNull(page);
        return index < page.Layouts.Length ? page.Layouts[index].Loader : index == page.Layouts.Length ? page.Loader : page.Views[index - page.FirstView].View.Loader;
    }

    private static LoaderOutcome[] Padded(LoaderOutcome[] results, PageDefinition page) =>
        results.Length >= page.ModelCount ? results : [.. results, .. new LoaderOutcome[page.ModelCount - results.Length]];

    private static async ValueTask<LoadedPage> RevalidateWithLoadersAsync(IServiceProvider services, PageDefinition page, PageRequest request, LoadedPage current, ImmutableArray<int> only, CancellationToken cancellationToken)
    {
        var load = PageLoad.For(services);
        var (loaded, keys) = await LoaderRuns.LoadAsync(services, page, request, load, cancellationToken, only);
        var results = Padded(loaded, page);
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = Merge(results, page, request.Path);
        if (outcome.Stops)
        {
            return current with { Outcome = outcome };
        }

        var models = current.Models.IsDefaultOrEmpty ? new object?[page.ModelCount] : current.Models.ToArray();
        PageCodeContext.Run(services, page, (services, page, only, results, models), static state => Refresh(state.services, state.page, state.only, state.results, state.models));

        keys = await ItemsAsync(services, page, request, load, models, results, keys, only.IsDefault ? default : [.. only.Where(slot => Repeated(page, slot))], current.Models, cancellationToken);
        await ModelLifecycles.ReportAsync(services, models, current.Models, request, cancellationToken);
        return new LoadedPage(ImmutableCollectionsMarshal.AsImmutableArray(models), outcome)
        {
            Views = page.Views.Length,
            Keys = PageKeys.After(keys, current.Keys, page.ModelCount, only.IsDefault ? null : index => LoaderRuns.Runs(page, only, index)),
        };
    }

    private static void Refresh(IServiceProvider services, PageDefinition page, ImmutableArray<int> only, LoaderOutcome[] results, object?[] models)
    {
        for (var index = 0; index < models.Length; index++)
        {
            if (LoaderAt(page, index) is { } loader && LoaderRuns.Runs(page, only, index) && !Repeated(page, index))
            {
                models[index] = Fresh(services, page, index, loader, results[index].Data, models[index]);
            }
        }
    }

    /// <summary>
    /// A slot's model after its loader ran again: the view's marker when it is not shown, the kept model when it takes the
    /// new data (it receives it), or a model created from the data.
    /// </summary>
    private static object? Fresh(IServiceProvider services, PageDefinition page, int index, LoaderDefinition loader, object? data, object? kept)
    {
        if (data is ViewState state)
        {
            return state;
        }

        if (loader.Receive is { } receive && kept is not null and not ViewState)
        {
            receive(kept, data);
            return kept;
        }

        return index < page.FirstView ? loader.CreateModel(services, data) : ViewModel(services, page, index, loader, data);
    }

    /// <summary>
    /// The slots a command in a view refreshes when it changed no key: that view alone (<see cref="PageKeys.Invalidated"/>
    /// adds the slots whose keys it invalidated).
    /// </summary>
    public static ImmutableArray<int> Invalidated(PageDefinition page, int view) => PageKeys.Invalidated(page, view, default, null);

    /// <summary>
    /// Whether a command that completed should refresh the given slots: there is one, and no model reports
    /// <see cref="INotifyDataErrorInfo.HasErrors"/>, the same rule as <see cref="Revalidates"/>.
    /// </summary>
    public static bool RevalidatesSlots(ImmutableArray<int> slots, ImmutableArray<object?> models) =>
        !slots.IsDefaultOrEmpty && !models.IsDefault && models.All(static model => model is not INotifyDataErrorInfo { HasErrors: true });

    /// <summary>
    /// Runs again only the loaders of the given view slots after a command in a view succeeded, each in a new scope and
    /// behind its error boundary, as LiveView sends an event to the one component that handles it
    /// (<c>phx-target={@myself}</c>): the page's, its layouts' and the other views' loaders do not run. A kept view model
    /// receives its fresh data; one that does not take data is created again.
    /// </summary>
    public static async ValueTask<LoadedPage> RevalidateViewsAsync(IServiceProvider services, PageDefinition page, PageRequest request, LoadedPage current, ImmutableArray<int> views, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        if (views.IsDefaultOrEmpty || current.Models.IsDefault)
        {
            return current;
        }

        var load = PageLoad.For(services);
        var (data, keys) = await LoaderRuns.LoadViewsAsync(services, page, request, load, views, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var models = current.Models.ToArray();
        foreach (var view in views.Where(view => !Repeated(page, view)))
        {
            models[view] = Fresh(services, page, view, LoaderAt(page, view)!, data[view].Data, models[view]);
        }

        keys = await ItemsAsync(services, page, request, load, models, data, keys, [.. views.Where(view => Repeated(page, view))], current.Models, cancellationToken);
        await ModelLifecycles.ReportAsync(services, models, current.Models, request, cancellationToken);
        return current with { Models = ImmutableCollectionsMarshal.AsImmutableArray(models), Keys = PageKeys.After(keys, current.Keys, page.ModelCount, views.Contains) };
    }

    /// <summary>
    /// Runs a repeated view's loader again for one item after a command in that item succeeded, with that item's input
    /// alone (a batch loader gets one key), in process or on the server for the installed app: the item's view model
    /// receives its data or is created again, and the other items keep theirs, as LiveView updates the one component that
    /// handled an event.
    /// </summary>
    public static async ValueTask<LoadedPage> RevalidateItemAsync(IServiceProvider services, PageDefinition page, PageRequest request, LoadedPage current, int slot, string item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        if (current.Models.IsDefault || current.Models[slot] is not ViewItems items || items.Find(item) is not { } entry || LoaderAt(page, slot) is not { } loader)
        {
            return current;
        }

        var (outcome, read) = await ItemResultAsync(services, page, request, slot, item, entry.Input, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = outcome.Data switch
        {
            ItemResults { Items: [var one] } => one.Outcome,
            ViewState state => new LoaderOutcome(state),
            _ => new LoaderOutcome(ViewState.Hidden),
        };
        var models = current.Models.SetItem(slot, items.With(item, ItemLoads.Model(services, page, slot, loader, result, entry.Model)));
        await ModelLifecycles.ReportAsync(services, models, current.Models, request, cancellationToken);
        var fresh = new ImmutableArray<string>[page.ModelCount];
        fresh[slot] = read.IsDefault ? [] : read;
        return current with { Models = models, Keys = PageKeys.After(fresh, current.Keys, page.ModelCount, index => index == slot) };
    }

    private static async ValueTask<ViewResult> ItemResultAsync(IServiceProvider services, PageDefinition page, PageRequest request, int slot, string item, object? input, CancellationToken cancellationToken)
    {
        if (services.GetService(typeof(ILoaderResults)) is ILoaderResults remote)
        {
            return await remote.LoadItemAsync(page, request, slot - page.FirstView, item, cancellationToken);
        }

        var (outcome, keys) = await ItemLoads.LoadItemAsync(services, page, request, slot, item, input, cancellationToken);
        return new ViewResult(outcome, keys);
    }

    /// <summary>
    /// Runs one view's loader behind its error boundary, as <see cref="LoadResultsAsync"/> runs a page's: what the server
    /// answers the installed app that asks for one view.
    /// </summary>
    public static ValueTask<ViewResult> LoadViewResultAsync(IServiceProvider services, PageDefinition page, PageRequest request, int view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        return LoaderRuns.LoadViewAsync(services, page, request, page.FirstView + view, cancellationToken);
    }

    /// <summary>
    /// Runs a repeated view's loader for one item behind its error boundary, with that item's key read from its text: what
    /// the server answers the installed app that asks for one item. A key that cannot be read from its text (an item without
    /// a declared key) is found among the items of the page's full results instead.
    /// </summary>
    public static async ValueTask<ViewResult> LoadItemResultAsync(IServiceProvider services, PageDefinition page, PageRequest request, int view, string item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        var slot = page.FirstView + view;
        if (page.Views[view].List?.Parse is { } parse)
        {
            var (outcome, keys) = await ItemLoads.LoadItemAsync(services, page, request, slot, item, parse(item), cancellationToken);
            return new ViewResult(outcome, keys.IsDefault ? [] : keys);
        }

        var results = await LoadResultsAsync(services, page, request, cancellationToken);
        var all = slot < results.Segments.Length ? results.Segments[slot] : default;
        var read = !results.Keys.IsDefaultOrEmpty && slot < results.Keys.Length ? results.Keys[slot] : [];
        return new ViewResult(all.Data is ItemResults items ? new LoaderOutcome(new ItemResults([.. items.Items.Where(each => each.Key == item)])) : all, read);
    }

    /// <summary>What the page and its layouts answer together; the views' results, after them, are never read here.</summary>
    private static PageOutcome Merge(LoaderOutcome[] results, PageDefinition page, string path)
    {
        var (notFound, redirect, title, absolute, metadata) = (false, (string?)null, (string?)null, false, (PageMetadata?)null);
        RemoteNotFound? notFoundPage = null;
        var (template, applied) = ((string?)null, (string?)null);
        for (var index = 0; index <= page.Layouts.Length; index++)
        {
            var head = index < page.Layouts.Length ? page.Layouts[index].Head : page.Head;
            var result = index < results.Length ? results[index] : default;
            notFound |= result.NotFound;
            notFoundPage ??= result.NotFound ? result.Data as RemoteNotFound : null;
            redirect ??= result.RedirectTo;
            if ((result.Title ?? head?.Title) is { } own)
            {
                (title, absolute, applied) = (own, result.Title is null && head!.AbsoluteTitle, template);
            }

            template = head?.TitleTemplate ?? template;
            metadata = Merge(Merge(metadata, head?.Metadata), result.Metadata);
        }

        title = title is null || absolute || applied is null ? title : applied.Replace("%s", title, StringComparison.Ordinal);
        return new PageOutcome(notFound, redirect is null ? null : PageNavigator.Resolve(path, redirect), title, metadata) { NotFoundPage = notFoundPage };
    }

    private static PageMetadata? Merge(PageMetadata? outer, PageMetadata? inner) =>
        outer is null || inner is null
            ? inner ?? outer
            : inner with
            {
                Description = inner.Description ?? outer.Description,
                Canonical = inner.Canonical ?? outer.Canonical,
                Image = inner.Image ?? outer.Image,
                Indexable = inner.Indexable && outer.Indexable,
                Alternates = inner.Alternates.IsDefaultOrEmpty ? outer.Alternates : inner.Alternates,
            };

    private static ValueTask<LoadedPage> LoadModelsAsync(object?[] models, int from, PageRequest request, PageOutcome? outcome, int views, CancellationToken cancellationToken)
    {
        for (var index = from; index < models.Length; index++)
        {
            if (models[index] is IPageModel model && PendingAsync(model.LoadAsync(request, cancellationToken)) is { } pending)
            {
                return ContinueAsync(pending, models, index + 1, request, outcome, views, cancellationToken);
            }
        }

        return new ValueTask<LoadedPage>(new LoadedPage(ImmutableCollectionsMarshal.AsImmutableArray(models), outcome) { Views = views });
    }

    private static async ValueTask<LoadedPage> ContinueAsync(Task pending, object?[] models, int next, PageRequest request, PageOutcome? outcome, int views, CancellationToken cancellationToken)
    {
        await pending;
        return await LoadModelsAsync(models, next, request, outcome, views, cancellationToken);
    }

    private static Task? PendingAsync(ValueTask loading) => loading.IsCompletedSuccessfully ? null : loading.AsTask();

    private static void Create(IServiceProvider services, PageDefinition page, LoaderOutcome[]? data, object?[] models, ImmutableArray<object?> kept = default) =>
        PageCodeContext.Run(services, page, (services, page, data, models, kept), static state => CreateModels(state.services, state.page, state.data, state.models, state.kept));

    private static void CreateModels(IServiceProvider services, PageDefinition page, LoaderOutcome[]? data, object?[] models, ImmutableArray<object?> kept)
    {
        var layouts = page.Layouts;
        for (var index = 0; index < models.Length; index++)
        {
            var loader = LoaderAt(page, index);
            if (index >= page.FirstView)
            {
                models[index] = ViewSlot(services, page, data, index, loader);
            }
            else if (loader is { Receive: { } receive } && !kept.IsDefault && index < kept.Length && kept[index] is { } model)
            {
                receive(model, data![index].Data);
                models[index] = model;
            }
            else if (loader is not null)
            {
                models[index] = loader.CreateModel(services, data![index].Data);
            }
            else
            {
                models[index] = index < layouts.Length ? layouts[index].CreateModel(services) : page.CreateModel(services);
            }
        }
    }

    /// <summary>A view slot's model once the loaders ran: its marker, nothing yet for a repeated view (its items load next), or its view model.</summary>
    private static object? ViewSlot(IServiceProvider services, PageDefinition page, LoaderOutcome[]? data, int index, LoaderDefinition? loader)
    {
        if (data is not null && data[index].Data is ViewState state)
        {
            return state;
        }

        return Repeated(page, index) ? null : ViewModel(services, page, index, loader, data?[index].Data);
    }

    private static object? ViewModel(IServiceProvider services, PageDefinition page, int index, LoaderDefinition? loader, object? data)
    {
        try
        {
            return loader is null ? page.Views[index - page.FirstView].View.CreateModel(services) : loader.CreateModel(services, data);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LoaderRuns.Log(services, page, index, error);
            return ViewState.Failed;
        }
    }

    /// <summary>
    /// Runs the loaders of the page and its layouts, as <see cref="LoadAsync(IServiceProvider, PageDefinition, PageRequest, CancellationToken)"/>
    /// does, without creating any model: what the server answers the installed app. A navigation asked of the page's
    /// navigator while they ran is the outcome's redirect when no loader redirected.
    /// </summary>
    public static async ValueTask<PageResults> LoadResultsAsync(IServiceProvider services, PageDefinition page, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        var navigator = Navigator(services);
        navigator?.Open(request.Path);
        var load = PageLoad.For(services);
        var (loaded, keys) = await LoaderRuns.LoadAsync(services, page, request, load, cancellationToken);
        var results = Padded(loaded, page);
        var outcome = Merge(results, page, request.Path);
        if (outcome.RedirectTo is null && navigator?.Requested is { } requested)
        {
            outcome = outcome with { RedirectTo = requested };
        }

        if (!outcome.Stops && page.Views.Any(static view => view.List is not null))
        {
            var models = new object?[page.ModelCount];
            Create(services, page, results, models);
            keys = await ItemLoads.LoadAsync(services, page, request, load, models, results, keys, default, cancellationToken);
        }

        return new PageResults(ImmutableCollectionsMarshal.AsImmutableArray(results), outcome) { Keys = PageKeys.After(keys, default, page.ModelCount) };
    }

    /// <summary>
    /// What the page answers instead of rendering, in this order: a loader's redirect (the outermost first), a navigation
    /// asked of its <see cref="Navigation.INavigator"/> while it loaded, a loader's not found, and then the deprecated
    /// <see cref="IPageModel.NotFound"/> and <see cref="IPageModel.RedirectTo"/>.
    /// </summary>
    public static PageAnswer Answer(IServiceProvider services, LoadedPage page)
    {
        if (page.Outcome?.RedirectTo is { } redirect)
        {
            return new PageAnswer(false, redirect);
        }

        if (Navigator(services)?.Requested is { } requested)
        {
            return new PageAnswer(false, requested);
        }

        return page.Outcome?.NotFound == true || PageModel(page)?.NotFound == true ? new PageAnswer(true, null) : new PageAnswer(false, RedirectTo(page.Models));
    }

    /// <summary>The page's title: its loaders', else its page model's.</summary>
    public static string Title(LoadedPage page) => page.Outcome?.Title ?? PageModel(page)?.Title ?? string.Empty;

    /// <summary>The page's search metadata: its loaders', else its page model's.</summary>
    public static PageMetadata Metadata(LoadedPage page) => page.Outcome?.Metadata ?? PageModel(page)?.Metadata ?? PageMetadata.None;

    public static IPageModel? PageModel(ImmutableArray<object?> models) => models.IsDefaultOrEmpty ? null : models[^1] as IPageModel;

    /// <summary>The page's own model, before its views' models, when it is an <see cref="IPageModel"/>.</summary>
    public static IPageModel? PageModel(LoadedPage page) =>
        page.Models.IsDefaultOrEmpty || page.Models.Length <= page.Views ? null : page.Models[^(page.Views + 1)] as IPageModel;

    /// <summary>The page's navigator, when the app registers one in the scope its models come from.</summary>
    public static PageNavigator? Navigator(IServiceProvider services) => services?.GetService(typeof(PageNavigator)) as PageNavigator;

    /// <summary>
    /// Where the page sends the visitor: the navigation asked of its <see cref="Navigation.INavigator"/> first, the
    /// deprecated <see cref="IPageModel.RedirectTo"/> otherwise.
    /// </summary>
    public static string? RedirectTo(IServiceProvider services, ImmutableArray<object?> models) =>
        Navigator(services)?.Requested ?? RedirectTo(models);

    /// <summary>Where the page or one of its layouts sends the visitor: the page first, then the layouts from the innermost out.</summary>
    public static string? RedirectTo(ImmutableArray<object?> models)
    {
        for (var index = models.IsDefault ? -1 : models.Length - 1; index >= 0; index--)
        {
            if (models[index] is IPageModel { RedirectTo: { } location })
            {
                return location;
            }
        }

        return null;
    }
}
