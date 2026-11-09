using System.Collections.Immutable;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// Pages in the visitor's culture, among the cultures the app supports: when the app configures ASP.NET Core's request
/// localization (<c>builder.Services.AddRequestLocalization(…)</c>, or <c>Aioros:Cultures</c>), its middleware runs first
/// in the pipeline after the forwarded headers and picks the culture from the query (<c>?culture=</c>), the
/// <c>.AspNetCore.Culture</c> cookie or <c>Accept-Language</c>, the default culture when none matches; the page renders in
/// it, its plans are kept per culture (<see cref="PageCultures"/>), and its answer says so with <c>Content-Language</c>,
/// <c>&lt;html lang&gt;</c> and <c>Vary</c>. An app that configures none renders in its own culture, as before.
/// </summary>
public static class LocalizedRequests
{
    private const string ContentLanguage = "Content-Language";

    /// <summary>Applies the app's request localization, when it configures one, before the pages read the request.</summary>
    public static IServiceCollection AddLocalizedRequests(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IStartupFilter, CultureFirst>();
        return services;
    }

    /// <summary>Whether the app configures ASP.NET Core's request localization.</summary>
    public static bool Localizes(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetServices<IConfigureOptions<RequestLocalizationOptions>>().Any();
    }

    /// <summary>
    /// The cultures pages are precompiled and warmed in: each supported culture with the UI culture of the same name, or
    /// the default UI culture, as a visitor asking for that culture gets them; none for an app that does not localize.
    /// </summary>
    public static ImmutableArray<RequestCulture> Supported(IServiceProvider services)
    {
        if (!Localizes(services))
        {
            return [];
        }

        var options = services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        var interfaces = options.SupportedUICultures ?? [];
        return [.. (options.SupportedCultures ?? []).Select(culture => new RequestCulture(culture, interfaces.FirstOrDefault(ui => ui.Name == culture.Name) ?? options.DefaultRequestCulture.UICulture))];
    }

    /// <summary>The language of the answer: the UI culture the request localization chose, else the app's language.</summary>
    internal static string Language(HttpContext http, SsrOptions options) =>
        http.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.Name is { Length: > 0 } name ? name : options.Language;

    /// <summary>
    /// Names the answer's language in <c>Content-Language</c> and what chose it in <c>Vary</c>: <c>Accept-Language</c>
    /// when the header or the default did, <c>Cookie</c> when the cookie did, nothing when the query did, whose address
    /// already tells the answers apart.
    /// </summary>
    internal static void Answer(HttpContext http)
    {
        if (http.Features.Get<IRequestCultureFeature>() is not { } chosen)
        {
            return;
        }

        var headers = http.Response.Headers;
        headers[ContentLanguage] = chosen.RequestCulture.UICulture.Name;
        var vary = chosen.Provider switch
        {
            QueryStringRequestCultureProvider => null,
            CookieRequestCultureProvider => "Cookie",
            _ => "Accept-Language",
        };
        if (vary is not null && !headers.Vary.Any(value => value is not null && value.Contains(vary, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Vary = headers.Vary.Count == 0 ? vary : headers.Vary + ", " + vary;
        }
    }

    private sealed class CultureFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            if (Localizes(app.ApplicationServices))
            {
                app.UseRequestLocalization();
                app.Use(static async (http, next) =>
                {
                    var chosen = http.Features.Get<IRequestCultureFeature>()!.RequestCulture;
                    PageCultures.Use(chosen.Culture, chosen.UICulture);
                    await next(http);
                });
            }

            next(app);
        };
    }
}
