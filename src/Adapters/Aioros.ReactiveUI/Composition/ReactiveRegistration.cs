using Aioros.Navigation;
using Aioros.Pages;
using Aioros.ReactiveUI.Activation;
using Aioros.ReactiveUI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReactiveUI;

namespace Aioros.ReactiveUI.Composition;

public static class ReactiveRegistration
{
    /// <summary>
    /// Registers the ReactiveUI adapter, scoped like the page's navigator: the page's <see cref="IScreen"/> (unless the app
    /// registers its own), whose navigations go to the routes declared here, and the lifecycle that activates, watches and
    /// shows the page's view models with what the generator found in them. The app calls the generated
    /// <c>AddReactiveRoutes(routes =&gt; …)</c>, which passes <paramref name="viewModels"/> itself.
    /// </summary>
    /// <param name="services">The app's services, which hold the page's <see cref="INavigator"/>.</param>
    /// <param name="viewModels">The generated table, <c>Aioros.Generated.AiorosReactiveViewModels.All</c>.</param>
    /// <param name="routes">Declares the route of each routable view model type.</param>
    public static IServiceCollection AddReactiveRoutes(this IServiceCollection services, ReactiveViewModels viewModels, Action<ReactiveRoutes> routes)
    {
        ArgumentNullException.ThrowIfNull(viewModels);
        ArgumentNullException.ThrowIfNull(routes);
        var table = new ReactiveRoutes();
        routes(table);
        services.AddSingleton(table);
        services.AddScoped(provider => new ReactivePage(provider.GetRequiredService<INavigator>(), table, viewModels));
        services.AddScoped<IModelLifecycle>(static provider => provider.GetRequiredService<ReactivePage>());
        services.TryAddScoped<IScreen>(static provider => new RouteScreen(provider.GetRequiredService<ReactivePage>()));
        return services;
    }
}
