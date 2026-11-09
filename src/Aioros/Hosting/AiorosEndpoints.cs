using Aioros.Hosting;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Functions;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Updates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Builder;

public static class AiorosEndpoints
{
    /// <summary>Maps the app's pages and server functions on the web; the desktop app shows its pages itself.</summary>
    /// <param name="app">The app.</param>
    /// <param name="pages">
    /// Who may open pages that no loader protects, as Razor Pages conventions:
    /// <c>pages =&gt; pages.AuthorizeFolder("/account").AllowAnonymousToPage("/account/login")</c>.
    /// </param>
    public static WebApplication MapAioros(this WebApplication app, Action<PageConventions>? pages = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Services.GetRequiredService<AiorosMode>().Desktop)
        {
            return app;
        }

        app.UseResponseCompression();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(errors => errors.Run(ErrorAsync));
        }

        if (Directory.Exists(app.Environment.WebRootPath))
        {
            app.UseStaticFiles();
        }

        app.UseUpdateFeed(app.Configuration[UpdateFeed.FolderSetting]);
        var aioros = app.Services.GetRequiredService<AiorosApp>();
        app.MapServerFunctions(aioros.Functions);
        AiorosSignIn.MapInstalledAppSignIn(app, aioros);

        var conventions = new PageConventions();
        pages?.Invoke(conventions);
        app.MapAiorosPages(aioros.Pages, conventions);
        app.MapGet(AiorosHost.FontsPath + "/{name}", FontAsync);
        app.MapGet(AiorosHost.FontsPath + "/r/{assembly}/{**path}", ResourceFontAsync);
        return app;
    }

    public static int RunAioros(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var mode = app.Services.GetRequiredService<AiorosMode>();
        if (!mode.Desktop)
        {
            app.Run();
            return Environment.ExitCode;
        }

        return AvaloniaEngine.RunDesktop(app, app.Services.GetRequiredService<AiorosApp>(), mode);
    }

    private static Task ErrorAsync(HttpContext http) => http.RequestServices.GetRequiredService<PageHandler>().ErrorAsync(http);

    private static async Task<IResult> ResourceFontAsync(string assembly, string path, HttpContext http)
    {
        var font = await AvaloniaEngine.ReadFontAsync(assembly, path, http.RequestAborted);
        if (font is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.Bytes(font, path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? "font/otf" : "font/ttf");
    }

    private static async Task<IResult> FontAsync(string name, HttpContext http)
    {
        var font = await WebFonts.ReadAsync(name, http.RequestAborted);
        if (font is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.Bytes(font, "font/woff2");
    }
}
