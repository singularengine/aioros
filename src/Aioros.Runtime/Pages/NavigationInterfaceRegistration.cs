using Aioros.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Runtime.Pages;

public static class NavigationInterfaceRegistration
{
    /// <summary>
    /// Registers the app's own navigation interface, scoped like the page's <see cref="INavigator"/>, with the
    /// implementation the generator emitted for it. The generator finds this call at build time.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="generated">The generated implementations, <c>Aioros.Generated.AiorosNavigators.All</c>.</param>
    public static IServiceCollection AddNavigation<TInterface>(this IServiceCollection services, IReadOnlyList<NavigationInterface> generated)
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(generated);
        var navigation = generated.FirstOrDefault(candidate => candidate.Interface == typeof(TInterface))
            ?? throw new InvalidOperationException($"{typeof(TInterface).Name} has no generated navigation; register it with AddNavigation in the app's project.");
        return services.AddScoped(provider => (TInterface)navigation.Create(provider.GetRequiredService<INavigator>()));
    }
}
