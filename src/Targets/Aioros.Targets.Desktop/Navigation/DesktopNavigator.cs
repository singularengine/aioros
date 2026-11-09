using System.Collections.Immutable;
using Aioros.Avalonia.Commands;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Hosting;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Routing.Tables;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Navigation;

public sealed class DesktopNavigator
{
    private const string ThemePrefix = "/theme/";

    private readonly Window _window;
    private readonly IServiceProvider _services;
    private readonly ImmutableArray<PageDefinition> _pages;
    private readonly PageDefinition? _notFound;
    private readonly PageDefinition? _error;
    private readonly RouteTable _routes;
    private readonly Stack<string> _history = new();
    private ImmutableArray<object?> _models = [];
    private ImmutableArray<ImmutableArray<string>> _keys = [];
    private readonly MountedViews _mounted;
    private IServiceScope? _scope;
    private ImmutableArray<IServiceScope?> _owners = [];
    private PageNavigator? _navigation;
    private PageDefinition? _page;
    private PageRequest? _request;
    private IServiceProvider? _pageServices;
    private CancellationTokenSource? _loading;
    private readonly ViewLoads _viewLoads = new();
    private readonly BuiltInSignIn? _signIn;
    private readonly LiveViews _live;
    private int _generation;

    public DesktopNavigator(Window window, XamlSources sources, IServiceProvider services, IEnumerable<PageDefinition> pages, PageDefinition? notFound, PageDefinition? error = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _mounted = new MountedViews(window, sources);
        _services = services;
        _pages = [.. pages];
        _notFound = notFound;
        _error = error;
        _routes = _pages.Aggregate(RouteTable.Empty, (table, page) => table.Add(page.Route));
        _signIn = services.GetService(typeof(SignInClient)) is SignInClient client ? new BuiltInSignIn(client, route => _routes.Match(route).IsMatch) : null;
        _live = new LiveViews(services, window, exception => FailAsync(exception, Current ?? "/"));
        LinkClicks.Attach(window, this);
        Motion.Reveals.Attach(window);
        Motion.Sweeps.Attach(window);
        Motion.Counts.Attach(window);
        Motion.Marquees.Attach(window);
        Motion.Spotlights.Attach(window);
        Motion.Parallaxes.Attach(window);
        window.AddHandler(Button.ClickEvent, OnClick, RoutingStrategies.Bubble, handledEventsToo: true);
        MenuCommands.Attach(window, this);
        DropCommands.Attach(window, this);
    }

    public event EventHandler<string>? Navigated;

    /// <summary>Raised when a navigation the user started fails; the window then shows the app's error page, if it has one.</summary>
    public event EventHandler<Exception>? Failed;

    /// <summary>
    /// Raised when a command a button ran has ended and what follows it is done: <see langword="true"/> when the page's
    /// loaders ran again and its models took the fresh data, <see langword="false"/> when nothing was revalidated (the
    /// command navigated, a model reports errors, or a newer navigation or revalidation superseded it).
    /// </summary>
    public event EventHandler<bool>? CommandSettled;

    /// <summary>Raised when a live page's views took fresh data after the keys their loaders read were invalidated.</summary>
    public event EventHandler? LiveRefreshed;

    public string? Current { get; private set; }

    /// <summary>Updates the app and restarts it on the path after a <c>426</c>; <see langword="false"/> when there is no update.</summary>
    public Func<string, Task<bool>>? Outdated { get; set; }

    /// <summary>Opens the installed app's sign-in in the system browser; Avalonia's launcher unless replaced.</summary>
    public Func<Uri, Task>? Browser { get; set; }

    public async Task NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        if (path.StartsWith(ThemePrefix, StringComparison.Ordinal))
        {
            DesktopRequests.SwitchTheme(path[ThemePrefix.Length..]);
            Navigated?.Invoke(this, path);
            return;
        }

        if (PageNavigator.IsExternal(path))
        {
            await LaunchAsync(path);
            return;
        }

        if (Current is not null)
        {
            _history.Push(Current);
        }

        await ShowAsync(path, cancellationToken);
    }

    public async Task<bool> BackAsync(CancellationToken cancellationToken = default)
    {
        if (!_history.TryPop(out var previous))
        {
            return false;
        }

        await ShowAsync(previous, cancellationToken);
        return true;
    }

    private async Task ShowAsync(string path, CancellationToken cancellationToken)
    {
        _live.Stop();
        var split = path.IndexOf('?', StringComparison.Ordinal);
        var route = split < 0 ? path : path[..split];
        var match = _routes.Match(route);
        var page = match.IsMatch ? _pages.First(candidate => candidate.Route == match.Template) : _notFound;
        var request = new PageRequest(route, match.Values.All, DesktopRequests.Query(split < 0 ? string.Empty : path[(split + 1)..]));
        if (_signIn?.Handles(route) == true)
        {
            await SignInAsync(_signIn, request, cancellationToken);
            return;
        }

        var scope = CreateScope();
        var services = scope?.ServiceProvider ?? _services;
        var (generation, token) = Begin(cancellationToken);
        LoadedPage loaded;
        PageAnswer answer;
        try
        {
            (page, loaded, answer) = await LoadAsync(services, page, request, generation, token);
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
            await PageMounts.ReleaseAsync(scope);
            return;
        }
        catch
        {
            await PageMounts.ReleaseAsync(scope);
            throw;
        }

        if (generation != _generation)
        {
            await PageMounts.ReleaseAsync(scope);
            return;
        }

        if (answer.RedirectTo is { } redirect)
        {
            await PageMounts.ReleaseAsync(scope);
            await (PageNavigator.IsExternal(redirect) ? LaunchAsync(redirect) : ShowAsync(redirect, cancellationToken));
            return;
        }

        Finish(generation);
        var owners = Owners(scope, loaded.Models);
        Show(page, loaded);
        await KeepAsync(scope, services, owners);
        (_page, _request) = (page, request);
        _live.Follow(page, _keys, RefreshLiveAsync);
        DesktopRequests.MarkCurrent(_window.Content as Control, route);
        Current = path;
        Navigated?.Invoke(this, path);
    }

    private async Task<(PageDefinition? Page, LoadedPage Loaded, PageAnswer Answer)> LoadAsync(IServiceProvider services, PageDefinition? page, PageRequest request, int generation, CancellationToken token)
    {
        var loaded = page is null ? default : await PageLoader.LoadAsync(services, page, request, PageMounts.Kept(page.Sources, _mounted.Shared(page.Sources), _models, _request, request), token);
        var answer = generation == _generation ? PageLoader.Answer(services, loaded) : default;
        if (generation != _generation || answer is not { NotFound: true, RedirectTo: null })
        {
            return (page, loaded, answer);
        }

        return _notFound is null ? (null, default, answer) : (_notFound, await PageLoader.LoadNotFoundAsync(services, _notFound, request, loaded, token), answer);
    }

    private (int Generation, CancellationToken Token) Begin(CancellationToken cancellationToken)
    {
        _viewLoads.CancelAll();
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return (++_generation, _loading.Token);
    }

    private void Finish(int generation)
    {
        if (generation == _generation)
        {
            _loading?.Dispose();
            _loading = null;
        }
    }

    /// <summary>
    /// Refreshes the views of the live page shown whose read keys an invalidation reached, from the app's data versions in
    /// process or the server's live stream: their loaders run again (through the data endpoint for the installed app) and
    /// their view models take the data, as after a command, unless a navigation is loading.
    /// </summary>
    private async Task RefreshLiveAsync(Invalidation invalidation)
    {
        if (_page is not { Live: true } page || _request is null || _pageServices is null || _loading is not null)
        {
            return;
        }

        var loaded = new LoadedPage(_models) { Keys = _keys };
        var slots = PageKeys.Reading(page, loaded, invalidation);
        try
        {
            if (!PageLoader.RevalidatesSlots(slots, _models) || !await RefreshSlotsAsync(page, loaded, slots))
            {
                return;
            }
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await FailAsync(exception, Current ?? "/");
            return;
        }

        LiveRefreshed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> RefreshSlotsAsync(PageDefinition page, LoadedPage loaded, ImmutableArray<int> slots)
    {
        if (PageKeys.ReachesPage(page, slots))
        {
            return await RevalidateAsync(slots);
        }

        var generation = _generation;
        var fresh = await PageLoader.RevalidateViewsAsync(_pageServices!, page, _request!, loaded, slots, CancellationToken.None);
        if (generation != _generation)
        {
            return false;
        }

        await TakeAsync(page, slots, fresh);
        return true;
    }

    private async Task<bool> RevalidateAsync(ImmutableArray<int> slots = default)
    {
        if (_page is null || _request is null || _pageServices is null || !PageLoader.Revalidates(_page, _models))
        {
            return false;
        }

        var (generation, token) = Begin(CancellationToken.None);
        LoadedPage fresh;
        try
        {
            var current = new LoadedPage(_models) { Keys = _keys };
            fresh = slots.IsDefault
                ? await PageLoader.RevalidateAsync(_pageServices, _page, _request, current, token)
                : await PageLoader.RevalidateSlotsAsync(_pageServices, _page, _request, current, slots, token);
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
            return false;
        }

        if (generation != _generation)
        {
            return false;
        }

        Finish(generation);
        if (fresh.Outcome is { Stops: true } outcome)
        {
            await (outcome.RedirectTo is { } redirect ? TryNavigateAsync(redirect) : ShowAsync(Current ?? "/", CancellationToken.None));
            return false;
        }

        _mounted.Rebind(_models, fresh.Models);
        _mounted.BindViews(_page, fresh.Models);
        var owners = Owners(_scope, fresh.Models);
        (_models, _keys) = (fresh.Models, fresh.Keys);
        await KeepAsync(_scope, _pageServices, owners);
        _live.Follow(_page, _keys, RefreshLiveAsync);
        _window.Title = PageLoader.Title(fresh) is { Length: > 0 } title ? title : _window.Title;
        return true;
    }

    /// <summary>
    /// Refreshes the views a command in a view changed: its own and those whose read keys it invalidated, each loader
    /// running again and each view model taking the data, or its control getting the new one, while the page, its layouts
    /// and the other views stay as they are; the whole page when a key the page or a layout read was invalidated, or when
    /// the command called a server function without key information. A command in one item of a repeated view refreshes
    /// that item alone, its loader running with that item's key. A newer command in the same view (or item) cancels this
    /// one and its result is discarded (newest wins per instance); a navigation cancels them all.
    /// </summary>
    private async Task<bool> RevalidateViewAsync(Runtime.Rendering.ViewTarget view, DataKeys? changes)
    {
        if (_page is not { } page || _request is null || _pageServices is null)
        {
            return false;
        }

        var loaded = new LoadedPage(_models) { Keys = _keys };
        var views = PageKeys.Invalidated(page, view.Slot, loaded, changes);
        var item = view.Item is not null && !PageKeys.Reaches(loaded, view.Slot, changes) ? view.Item : null;
        var others = item is null ? views : views.Remove(view.Slot);
        if (!PageLoader.RevalidatesSlots(views, _models))
        {
            return false;
        }

        if (PageKeys.ReachesPage(page, others))
        {
            return await RevalidateAsync(views);
        }

        var generation = _generation;
        var loading = await _viewLoads.StartAsync(view);
        var token = loading.Token;
        LoadedPage fresh;
        try
        {
            fresh = await RefreshAsync(page, loaded, others, view.Slot, item, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }

        if (token.IsCancellationRequested || generation != _generation)
        {
            return false;
        }

        _viewLoads.Finish(view, loading);
        await TakeAsync(page, views, fresh);
        return true;
    }

    private async Task<LoadedPage> RefreshAsync(PageDefinition page, LoadedPage loaded, ImmutableArray<int> others, int slot, string? item, CancellationToken token)
    {
        var fresh = others.IsEmpty ? loaded : await PageLoader.RevalidateViewsAsync(_pageServices!, page, _request!, loaded, others, token);
        return item is null ? fresh : await PageLoader.RevalidateItemAsync(_pageServices!, page, _request!, fresh, slot, item, token);
    }

    /// <summary>Takes the refreshed views' models and keys, and binds them to their controls.</summary>
    private async Task TakeAsync(PageDefinition page, ImmutableArray<int> views, LoadedPage fresh)
    {
        var models = _models.ToBuilder();
        var keys = _keys.IsDefaultOrEmpty ? fresh.Keys.ToBuilder() : _keys.ToBuilder();
        foreach (var slot in views)
        {
            models[slot] = fresh.Models[slot];
            if (slot < keys.Count)
            {
                keys[slot] = slot < fresh.Keys.Length ? fresh.Keys[slot] : [];
            }
        }

        var current = models.ToImmutable();
        var owners = Owners(_scope, current);
        (_models, _keys) = (current, keys.ToImmutable());
        _mounted.BindViews(page, current);
        await KeepAsync(_scope, _pageServices, owners);
        _live.Follow(page, _keys, RefreshLiveAsync);
    }

    private IServiceScope? CreateScope() => (_services.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory)?.CreateScope();

    private ImmutableArray<IServiceScope?> Owners(IServiceScope? scope, ImmutableArray<object?> models) =>
        models.IsDefault
            ? []
            : [.. models.Select((model, index) => model is not null && index < _models.Length && index < _owners.Length && ReferenceEquals(model, _models[index]) ? _owners[index] : scope)];

    private async Task KeepAsync(IServiceScope? scope, IServiceProvider? services, ImmutableArray<IServiceScope?> owners)
    {
        var released = _owners.Append(_scope).Except(owners.Append(scope)).OfType<IServiceScope>().Distinct().ToList();
        (_scope, _owners) = (scope, owners);
        if (!ReferenceEquals(_pageServices, services))
        {
            _pageServices = services;
            _navigation = services is null ? null : PageLoader.Navigator(services);
            _navigation?.Follow(path => new ValueTask(TryNavigateAsync(path)));
        }

        foreach (var old in released)
        {
            await PageMounts.ReleaseAsync(old);
        }
    }

    private Task LaunchAsync(string url) => PageMounts.LaunchAsync(_window, url);

    private void Show(PageDefinition? page, LoadedPage loaded)
    {
        var models = loaded.Models.IsDefault ? [] : loaded.Models;
        (_models, _keys) = (models, loaded.Keys);
        _mounted.Show(page, models);
        _window.Title = PageLoader.Title(loaded) is { Length: > 0 } title ? title : _window.Title;
    }

    /// <summary>
    /// Loads the views again from the first that shows <paramref name="source"/> (every view when null) with the models the
    /// page has, after hot reload loaded an edit over the compiled XAML or applied a C# edit (DESKTOP-98); false when the
    /// window does not show that source.
    /// </summary>
    public bool Reload(string? source = null) => _mounted.Reload(_page, _models, source);

    private void OnClick(object? sender, RoutedEventArgs args)
    {
        Task<DataKeys?> run;
        if (args is { Handled: false, Source: Button { Command: { } command } button } && command.CanExecute(button.CommandParameter))
        {
            args.Handled = true;
            run = RunAsync(command, button.CommandParameter);
        }
        else
        {
            run = Task.FromResult<DataKeys?>(null);
        }

        Follow(run, args.Source is Control source ? PageComposer.ViewTarget(source) : Runtime.Rendering.ViewTarget.Page);
    }

    internal void Run(System.Windows.Input.ICommand command, object? parameter, Control source) => Follow(RunAsync(command, parameter), PageComposer.ViewTarget(source));

    private void Follow(Task<DataKeys?> run, Runtime.Rendering.ViewTarget view)
    {
        var generation = _generation;
        Dispatcher.UIThread.Post(() => _ = FollowRedirectAsync(run, generation, view));
    }

    /// <summary>Runs a command, recording what its server functions changed (<see cref="DataKeys"/>) in its own flow.</summary>
    private static async Task<DataKeys?> RunAsync(System.Windows.Input.ICommand command, object? parameter)
    {
        var changes = DataKeys.Begin();
        await AwaitedCommands.RunAsync(command, parameter);
        return changes;
    }

    private async Task FollowRedirectAsync(Task<DataKeys?> run, int generation, Runtime.Rendering.ViewTarget view)
    {
        bool revalidated;
        try
        {
            var changes = await run;
            revalidated = await FollowAsync(generation, view, changes);
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await FailAsync(exception, Current ?? "/");
            return;
        }

        CommandSettled?.Invoke(this, revalidated);
    }

    private async Task<bool> FollowAsync(int generation, Runtime.Rendering.ViewTarget view, DataKeys? changes)
    {
        if (generation != _generation || _navigation?.Requested is not null)
        {
            return false;
        }

        if (PageLoader.RedirectTo(_models) is { } redirect)
        {
            await TryNavigateAsync(redirect);
            return false;
        }

        return view.Slot >= 0 ? await RevalidateViewAsync(view, changes) : await RevalidateAsync();
    }

    /// <summary>
    /// Navigates without letting a failure vanish: the error is raised through <see cref="Failed"/> and the app's error
    /// page is shown, as the website answers 500 with it.
    /// </summary>
    public async Task TryNavigateAsync(string path)
    {
        try
        {
            await NavigateAsync(path, CancellationToken.None);
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await FailAsync(exception, path);
        }
    }

    private async Task SignInAsync(BuiltInSignIn signIn, PageRequest request, CancellationToken cancellationToken)
    {
        var (generation, _) = Begin(cancellationToken);
        if (await signIn.RunAsync(request, ShowBuiltInAsync, _loading!, Browser ?? (address => LaunchAsync(address.AbsoluteUri)), () => generation != _generation, path => _ = TryNavigateAsync(path)) is { } next)
        {
            await ShowAsync(next, cancellationToken);
        }
    }

    private async Task FailAsync(Exception exception, string path)
    {
        if (_signIn?.Redirect(exception, Current, path) is { } signIn)
        {
            await TryNavigateAsync(signIn);
            return;
        }

        if (exception is ClientOutdatedException && Outdated is { } outdated && await outdated(path))
        {
            return;
        }

        Failed?.Invoke(this, exception);
        await ShowErrorAsync(path);
    }

    private async Task ShowErrorAsync(string path)
    {
        _live.Stop();
        var (generation, token) = Begin(CancellationToken.None);
        if (_error is not null)
        {
            var scope = CreateScope();
            var services = scope?.ServiceProvider ?? _services;
            var request = new PageRequest(path, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);
            try
            {
                var loaded = await PageLoader.LoadAsync(services, _error, request, token);
                Finish(generation);
                var owners = Owners(scope, loaded.Models);
                Show(_error, loaded);
                await KeepAsync(scope, services, owners);
                (_page, _request) = (_error, request);
                return;
            }
#pragma warning disable CA1031
            catch (Exception)
#pragma warning restore CA1031
            {
                await PageMounts.ReleaseAsync(scope);
            }
        }

        Finish(generation);
        await ShowBuiltInAsync(BuiltInError.Create());
    }

    private async Task ShowBuiltInAsync(Control content)
    {
        _live.Stop();
        PageMounts.Unmount(_window, null);
        _window.Content = content;
        _mounted.Clear();
        (_models, _keys) = ([], []);
        await KeepAsync(null, null, []);
        (_page, _request) = (null, null);
    }

}
