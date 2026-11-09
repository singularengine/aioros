using System.Collections.Immutable;
using System.Collections.Specialized;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.ReactiveUI.Navigation;
using ReactiveUI;

namespace Aioros.ReactiveUI.Activation;

/// <summary>
/// The ReactiveUI view models of one page, scoped like its models: one per request on the web, one per page shown on the
/// desktop. It activates each view model once when the page creates it and deactivates it when the scope ends, watches
/// every <see cref="RoutingState"/> of the page so a navigation goes to the route of the view model navigated to through
/// the page's navigator, and puts the page's routable view model on those routing states as their current one.
/// </summary>
public sealed class ReactivePage(INavigator navigator, ReactiveRoutes routes, ReactiveViewModels viewModels) : IModelLifecycle, IDisposable
{
    private ImmutableHashSet<object> _seen = ImmutableHashSet.Create<object>(ReferenceEqualityComparer.Instance);
    private ImmutableList<IDisposable> _ends = [];
    private ImmutableList<RoutingState> _routers = [];
    private int _showing;

    public ValueTask CreatedAsync(object model, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (ImmutableInterlocked.Update(ref _seen, static (seen, model) => seen.Add(model), model) && viewModels.Attach(model, this) is { } end)
        {
            Keep(end);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Follows the navigations of a routing state until the page ends.</summary>
    public void Watch(RoutingState? router)
    {
        if (router is null || !ImmutableInterlocked.Update(ref _routers, static (routers, router) => routers.Contains(router) ? routers : routers.Add(router), router))
        {
            return;
        }

        var stack = router.NavigationStack;
        stack.CollectionChanged += Navigated;
        Keep(new Unwatch(stack, Navigated));
    }

    /// <summary>Makes a routable view model the current one of every routing state the page watches, without navigating.</summary>
    public void Show(IRoutableViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Interlocked.Increment(ref _showing);
        try
        {
            foreach (var stack in Volatile.Read(ref _routers).Select(router => router.NavigationStack))
            {
                stack.Clear();
                stack.Add(model);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _showing);
        }
    }

    public void Dispose()
    {
        foreach (var end in Interlocked.Exchange(ref _ends, []).Reverse())
        {
            end.Dispose();
        }
    }

    private void Keep(IDisposable end) => ImmutableInterlocked.Update(ref _ends, static (ends, end) => ends.Add(end), end);

    private void Navigated(object? sender, NotifyCollectionChangedEventArgs change)
    {
        if (Volatile.Read(ref _showing) > 0 || change.Action != NotifyCollectionChangedAction.Add || change.NewItems is not { Count: 1 } added || added[0] is not IRoutableViewModel target)
        {
            return;
        }

        var path = routes.PathOf(target)
            ?? throw new InvalidOperationException($"{target.GetType().Name} has no route: declare it with Map<{target.GetType().Name}>(path) in AddReactiveRoutes.");
        _ = navigator.NavigateAsync(new NavigationRequest(path), CancellationToken.None).AsTask();
    }

    private sealed class Unwatch(INotifyCollectionChanged stack, NotifyCollectionChangedEventHandler handler) : IDisposable
    {
        public void Dispose() => stack.CollectionChanged -= handler;
    }
}
