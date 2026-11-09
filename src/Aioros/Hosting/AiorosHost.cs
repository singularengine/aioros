using System.Globalization;
using Aioros.Development;
using Aioros.Events;
using Aioros.Navigation;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Events;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Hosting;

public static partial class AiorosHost
{
    public const string FontsPath = "/_aioros/fonts";

    /// <summary>The configuration key of the page cache's size in bytes; 0 turns the page cache off.</summary>
    public const string PageCacheSetting = "Aioros:PageCacheBytes";

    /// <summary>Where the native-only stand-in's "Download" link points, <c>/download</c> unless set.</summary>
    public const string DownloadSetting = "Aioros:Download";

    public const string UploadLimitSetting = "Aioros:UploadLimit";

    /// <summary>The theme a visitor who has chosen none gets: <c>light</c> (the default), <c>dark</c> or <c>system</c>.</summary>
    public const string ThemeSetting = "Aioros:Theme";

    /// <summary>
    /// The configuration key of the cultures the website's pages render in, separated by commas, the first being the
    /// default (<c>Aioros__Cultures=en-US,fr-FR</c>): ASP.NET Core's request localization over those cultures, as the app
    /// would configure it with <c>AddRequestLocalization</c>. The desktop app keeps the operating system's culture.
    /// </summary>
    public const string CulturesSetting = "Aioros:Cultures";

    public static WebApplicationBuilder CreateBuilder(string[] args, AiorosApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var firstRun = AiorosMode.IsInstalled && AvaloniaEngine.StartInstalled(args, app.Scheme);
        var builder = WebApplication.CreateBuilder(AiorosMode.Settings(args));
        var development = builder.Environment.IsDevelopment();
        var mode = AiorosMode.From(args, builder.Configuration, app.Server, development, AiorosMode.IsInstalled) with { FirstRun = firstRun };
        var services = builder.Services;
        services.AddSingleton(app);
        services.AddSingleton(mode);
        services.AddSingleton<IEventBus, EventBus>();
        Register(app);
        if (mode.Desktop)
        {
            services.AddPageNavigator();
            services.AddSingleton<Microsoft.AspNetCore.Hosting.Server.IServer, NoServer>();
            AvaloniaEngine.AddDesktop(services);
            services.AddRemoteLoaderResults(mode.Server);
            services.AddDataKeys();
            return builder;
        }

        var options = Options(app, development, builder.Configuration[PageCacheSetting], builder.Configuration[DownloadSetting]);
        options.Icon = Icon(builder.Environment);
        options.UploadLimit = Bytes(builder.Configuration[UploadLimitSetting], SsrOptions.DefaultUploadLimit);
        options.Theme = builder.Configuration[ThemeSetting] is { } theme && theme is "dark" or "system" ? theme : "light";
        options.DevelopmentToken = development ? builder.Configuration[DevelopmentTokenSetting] : null;
        services.AddAiorosPages(options);
        Cultures(services, builder.Configuration[CulturesSetting]);
        AddEngine(services, app, development, mode);
        services.AddSingleton<IPageEngine>(provider => provider.GetRequiredService<DeferredPageEngine>());
        services.AddSingleton<Pages.IUiThread>(provider => provider.GetRequiredService<DeferredPageEngine>());
        services.AddSingleton<IPageCapture>(provider => provider.GetRequiredService<DeferredPageEngine>());
        services.AddSingleton<IFieldResolver>(new PlanSlotResolver(EngineFallback.Instance));
        services.AddHostedService<PlanInvalidation>();
        if (development)
        {
            services.AddHostedService<CodeForwarding>();
            WatchDevelopmentTool(services, options, builder.Configuration[DevelopmentToolSetting]);
        }

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
        return builder;
    }

    /// <summary>The configuration key of the token <c>aioros dev</c> gives the app to post the build's errors, read in development only.</summary>
    public const string DevelopmentTokenSetting = "Aioros:DevelopmentToken";

    /// <summary>The configuration key of the process id of the <c>aioros dev</c> that started the app, read in development only.</summary>
    public const string DevelopmentToolSetting = "Aioros:DevelopmentTool";

    private static void WatchDevelopmentTool(IServiceCollection services, SsrOptions options, string? tool)
    {
        if (int.TryParse(tool, NumberStyles.None, CultureInfo.InvariantCulture, out var process))
        {
            options.DevelopmentTool = process;
            services.AddHostedService<Targets.Ssr.Development.DevelopmentToolWatch>();
        }
    }

    private static SsrOptions Options(AiorosApp app, bool development, string? pageCacheBytes, string? download) => new()
    {
        NativeOnly = app.NativeOnly,
        Download = download ?? "/download",
        Development = development,
        ExternalCss = !development,
        HeadCss = WebFonts.Css(FontsPath),
        NotFound = app.NotFound,
        Error = app.Error,
        AppScheme = app.Scheme,
        PageCacheBytes = Bytes(pageCacheBytes, Targets.Ssr.Caching.PageCache.DefaultMaxBytes),
    };

    private static void Cultures(IServiceCollection services, string? setting)
    {
        if (setting?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is not { Length: > 0 } names)
        {
            return;
        }

        services.AddRequestLocalization(options => options.SetDefaultCulture(names[0]).AddSupportedCultures(names).AddSupportedUICultures(names));
    }

    private static string? Icon(Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment) =>
        Targets.Ssr.Search.AppIcon.Find(name => environment.WebRootFileProvider.GetFileInfo(name).Exists);

    private static long Bytes(string? setting, long fallback) =>
        long.TryParse(setting, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var bytes) ? bytes : fallback;

    internal static AiorosApp App(WebApplicationBuilder builder) => Registered<AiorosApp>(builder);

    internal static AiorosMode Mode(WebApplicationBuilder builder) => Registered<AiorosMode>(builder);

    private static T Registered<T>(WebApplicationBuilder builder)
        where T : class =>
        builder.Services.LastOrDefault(service => service.ServiceType == typeof(T))?.ImplementationInstance as T
        ?? throw new InvalidOperationException($"The builder has no {typeof(T).Name}; create it with AiorosApplication.CreateBuilder.");

    /// <summary>Hands the app's generated code to the runtime: member accessors, types, command shapes, converters, template selectors, slot writers and icons.</summary>
    private static void Register(AiorosApp app)
    {
        MemberAccessors.Register(app.Accessors);
        MemberAccessors.Defer(app.DeferredAccessors);
        PlanTypes.Register(app.Types);
        PlanCommands.Register(app.Commands);
        PlanConverters.Register(app.Converters);
        PlanSelectors.Register(app.Selectors);
        Aioros.Runtime.Rendering.SlotWriters.Register(app.SlotWriters);
        Aioros.Runtime.Slots.IconSet.Register(app.Icons);
    }

    private static void AddEngine(IServiceCollection services, AiorosApp app, bool development, AiorosMode mode)
    {
        if (mode.Precompile is { } target)
        {
            services.AddSingleton<Microsoft.AspNetCore.Hosting.Server.IServer, NoServer>();
            AvaloniaEngine.AddPrecompiling(services, app);
            services.AddHostedService(provider => new PlanExport(target, provider.GetRequiredService<AiorosApp>(), provider, provider.GetRequiredService<PageHandler>(), provider.GetRequiredService<IHostApplicationLifetime>()));
            return;
        }

        var precompiled = !development && File.Exists(PlanExport.PlansPath);
        if (precompiled)
        {
            AvaloniaEngine.AddDeferred(services, app);
            services.AddHostedService<PlanLoad>();
        }
        else
        {
            AvaloniaEngine.AddStarted(services, app, development);
        }

        services.AddHostedService<PlanWarmup>();
    }

    private sealed class PlanWarmup(AiorosApp app, IServiceProvider services, PageHandler handler, Microsoft.Extensions.Logging.ILogger<PlanWarmup> log) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
            var started = clock.GetTimestamp();
            await using var scope = services.CreateAsyncScope();
            var outcomes = await Runtime.Rendering.BackgroundWork.RunAsync(() => Warmup.RunAsync(PlanExport.Pages(app), scope.ServiceProvider, handler, posts: true, stoppingToken));
            Warmup.Report(log, outcomes, clock.GetElapsedTime(started));
        }
    }

    private sealed class PlanLoad(AiorosApp app, PlanCache cache, SsrOptions options) : IHostedLifecycleService
    {
        public async Task StartingAsync(CancellationToken cancellationToken) =>
            cache.Preload(await PlanExport.ReadAsync(cancellationToken), PlanExport.Pages(app), options);

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CodeForwarding(IEventBus bus) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Development.CodeUpdates.ForwardAsync(bus, stoppingToken);
    }

    private sealed class PlanInvalidation(IEventBus bus, PlanCache cache) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Targets.Ssr.Development.DevelopmentChannel.InvalidateAsync(bus, cache, stoppingToken);
    }

}
