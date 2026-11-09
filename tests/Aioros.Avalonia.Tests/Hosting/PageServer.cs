using Aioros.Avalonia.Tests.Models;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Avalonia.Tests.Hosting;

internal static class PageServer
{
    public const string ErrorTitle = "Something went wrong";

    public static Task<WebApplication> StartAsync(EngineFixture fixture, params PageDefinition[] pages) => StartAsync(fixture, fixture.Engine, null, pages);

    /// <summary>A server whose posts the plan answers as an app's does, through the adapter's field resolver, beside <paramref name="engine"/>.</summary>
    public static Task<WebApplication> StartPlannedAsync(EngineFixture fixture, IPageEngine engine, params PageDefinition[] pages) => StartAsync(fixture, engine, new Plans.AdapterSlotResolver(), pages);

    private static async Task<WebApplication> StartAsync(EngineFixture fixture, IPageEngine engine, IFieldResolver? fields, PageDefinition[] pages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(fixture.Logs);
        builder.Services.AddAiorosPages(new SsrOptions { Error = new PageDefinition("/_error", "Pages/Simple/Page.axaml", static _ => new Article(ErrorTitle), []) })
            .AddSingleton(engine);
        if (fields is not null)
        {
            builder.Services.AddSingleton(fields);
        }

        var app = builder.Build();
        app.UseExceptionHandler(static errors => errors.Run(static http => http.RequestServices.GetRequiredService<PageHandler>().ErrorAsync(http)));
        app.MapAiorosPages(pages);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public static PageDefinition Page(string route, string folder, Func<IServiceProvider, object?> model) => new(route, $"Pages/{folder}/Page.axaml", model, []);
}
