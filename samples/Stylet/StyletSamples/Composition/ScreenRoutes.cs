using System.Collections.Immutable;
using Aioros.Navigation;
using Aioros.Pages;
using Stylet.Avalonia;
using Stylet.Samples.NavigationController;
using Stylet.Samples.NavigationController.Pages;

namespace StyletSamples.Composition;

public sealed class RouteNavigationController(INavigator navigator) : INavigationController
{
    public void NavigateToPage1() => Navigate("/navigation/page1");

    public void NavigateToPage2(string initiator) => Navigate($"/navigation/page2?initiator={Uri.EscapeDataString(initiator)}");

    private void Navigate(string path) => _ = navigator.NavigateAsync(new NavigationRequest(path), CancellationToken.None).AsTask();
}

public sealed class ScreenLifecycle : IModelLifecycle, IDisposable
{
    private ImmutableList<object> _screens = [];

    public ValueTask CreatedAsync(object model, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (model is Page2ViewModel page2)
        {
            page2.Initiator = request.Value("initiator");
        }

        ScreenExtensions.TryActivate(model);
        ImmutableInterlocked.Update(ref _screens, static (all, added) => all.Add(added), model);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var screen in Interlocked.Exchange(ref _screens, []))
        {
            ScreenExtensions.TryClose(screen);
        }
    }
}
