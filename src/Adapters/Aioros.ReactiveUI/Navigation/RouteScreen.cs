using Aioros.ReactiveUI.Activation;
using ReactiveUI;
using ReactiveUI.Primitives.Concurrency;

namespace Aioros.ReactiveUI.Navigation;

/// <summary>
/// The page's <see cref="IScreen"/>, which view models receive from DI: a navigation of its <see cref="Router"/> goes to the
/// route of the view model navigated to, through the page's navigator.
/// </summary>
public sealed class RouteScreen : IScreen
{
    public RouteScreen(ReactivePage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Watch(Router);
    }

    public RoutingState Router { get; } = new(Sequencer.Immediate);
}
