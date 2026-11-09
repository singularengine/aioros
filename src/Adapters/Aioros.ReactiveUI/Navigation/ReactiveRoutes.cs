using System.Collections.Immutable;
using ReactiveUI;

namespace Aioros.ReactiveUI.Navigation;

/// <summary>
/// The route of each routable view model type: ReactiveUI navigates to an instance, which a page cannot be addressed
/// by, so each type declares the path its instances are shown at, from the instance's state when the route has values.
/// </summary>
public sealed class ReactiveRoutes
{
    private ImmutableDictionary<Type, Func<IRoutableViewModel, string>> _paths = ImmutableDictionary<Type, Func<IRoutableViewModel, string>>.Empty;

    /// <summary>Shows every <typeparamref name="TViewModel"/> navigated to at <paramref name="path"/>.</summary>
    public ReactiveRoutes Map<TViewModel>(string path)
        where TViewModel : IRoutableViewModel
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Map<TViewModel>(_ => path);
    }

    /// <summary>Shows a <typeparamref name="TViewModel"/> navigated to at the path made from it, such as <c>album =&gt; $"/albums/{album.Id}"</c>.</summary>
    public ReactiveRoutes Map<TViewModel>(Func<TViewModel, string> path)
        where TViewModel : IRoutableViewModel
    {
        ArgumentNullException.ThrowIfNull(path);
        _paths = _paths.SetItem(typeof(TViewModel), model => path((TViewModel)model));
        return this;
    }

    /// <summary>The path a view model navigated to is shown at, or <see langword="null"/> when its type is not mapped.</summary>
    public string? PathOf(IRoutableViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _paths.TryGetValue(model.GetType(), out var path) ? path(model) : null;
    }
}
