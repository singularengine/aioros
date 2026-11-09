using Aioros.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Runtime.Pages;

public static class PageNavigatorRegistration
{
    /// <summary>
    /// Registers the page's <see cref="INavigator"/>, scoped: one per request on the web and one per page on the desktop,
    /// so a view model or loader never navigates another user's page. Any container receives it through
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    public static IServiceCollection AddPageNavigator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped(static _ => new PageNavigator());
        return services.AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>());
    }
}
