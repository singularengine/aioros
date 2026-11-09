using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Targets.Ssr.Caching;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

public static class PageServices
{
    /// <summary>
    /// Registers what the website needs to serve its pages, beside an <see cref="IPageEngine"/>: the options, the plan
    /// cache, the page cache for pages whose loaders declare a lifetime, the app's data keys (<see cref="Aioros.Functions.IDataKeys"/>,
    /// which a background service invalidates to refresh live pages), the page handler, the request's
    /// <see cref="Navigation.INavigator"/>, and the forwarded headers of trusted proxies (<see cref="ProxiedRequests"/>). Every registration goes through
    /// <see cref="IServiceCollection"/>, so a container behind <c>UseServiceProviderFactory</c> receives them.
    /// </summary>
    public static IServiceCollection AddAiorosPages(this IServiceCollection services, SsrOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(options);
        services.AddProxiedRequests();
        services.AddEngineCapacity();
        services.AddContentSecurity();
        DataProtectionStateSeal.AddStateSeal(services);
        services.AddLocalizedRequests();
        services.AddSingleton(static _ => new PlanCache());
        services.AddDataKeys();
        services.AddSingleton(static services => new PageCache(services.GetRequiredService<DataVersions>(), services.GetService<TimeProvider>() ?? TimeProvider.System, services.GetRequiredService<SsrOptions>().PageCacheBytes));
        services.AddSingleton(static services => new PageHandler(
            services.GetRequiredService<IPageEngine>(),
            services.GetRequiredService<PlanCache>(),
            services.GetRequiredService<SsrOptions>(),
            services.GetService<IFieldResolver>(),
            services.GetService<CrossSite.CrossOriginProtection>(),
            services.GetRequiredService<PageCache>()));
        services.AddSingleton<IViewAccess, Access.AuthorizationViewAccess>();
        if (options.Development)
        {
            services.AddSingleton<Microsoft.AspNetCore.Diagnostics.IDeveloperPageExceptionFilter, Development.DevelopmentFaults>();
        }

        return services.AddPageNavigator();
    }

    /// <summary>
    /// Turns ASP.NET Core's antiforgery tokens on for pages: forms with fields carry the token and posts without a valid
    /// one are refused with <c>400</c>. Off by default, since the cross-origin check already refuses cross-site posts and
    /// the token costs a cookie and a cryptographic operation on every rendered form.
    /// </summary>
    public static IServiceCollection AddAntiforgeryTokens(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.LastOrDefault(static service => service.ServiceType == typeof(SsrOptions))?.ImplementationInstance as SsrOptions
            ?? throw new InvalidOperationException("Register the pages with AddAiorosPages before turning antiforgery tokens on.");
        options.Antiforgery = true;
        return services.AddAntiforgery();
    }
}
