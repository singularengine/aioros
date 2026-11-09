using System.Runtime.CompilerServices;
using Aioros.Avalonia.Hosting;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Hosting;

/// <summary>
/// Everything the host asks of the Avalonia adapter, kept apart so that only a method that runs touches it: the .NET
/// runtime loads an assembly when it compiles a method that names one of its types, so production, which calls only
/// <see cref="AddDeferred"/> and the fallback's loader, loads neither the adapter nor Avalonia until a page needs them.
/// Each method is kept from being inlined into its caller, which would name the adapter's types there. With the
/// <see cref="Pages.PageEngines.AvaloniaSwitch"/> feature switch off (a Native AOT publish), the trimmer removes every
/// path to Avalonia: the app serves its precompiled pages and posts only, and the desktop does not run.
/// </summary>
internal static class AvaloniaEngine
{
    private const string Unsupported = "The Avalonia engine was left out of this build (" + Pages.PageEngines.AvaloniaSwitch + " is off): only pages and posts the plans file covers are served, and the desktop does not run.";

    /// <summary>A font file of an assembly's Avalonia resources, read without starting Avalonia (the page's <c>@font-face</c> sources).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ValueTask<byte[]?> ReadFontAsync(string assembly, string path, CancellationToken cancellationToken) =>
        AvaloniaResourceFiles.ReadFontAsync(assembly, path, cancellationToken);

    /// <summary>Registers what the desktop host runs on: its UI thread.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddDesktop(IServiceCollection services)
    {
        if (!Pages.PageEngines.AvaloniaIsSupported)
        {
            throw new NotSupportedException(Unsupported);
        }

        services.AddSingleton<Pages.IUiThread, Targets.Desktop.Hosting.DesktopUiThread>();
    }

    /// <summary>Runs the desktop app until its window closes.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int RunDesktop(Microsoft.AspNetCore.Builder.WebApplication app, AiorosApp aioros, AiorosMode mode)
    {
        if (!Pages.PageEngines.AvaloniaIsSupported)
        {
            throw new NotSupportedException(Unsupported);
        }

        return Targets.Desktop.Hosting.DesktopHost.Run(
            new Targets.Desktop.Hosting.DesktopApp(aioros.ApplicationType?.Invoke(), aioros.Assembly, app.Services, aioros.Pages, aioros.NotFound, mode.StartPath, app.StartAsync, app.StopAsync)
            {
                Error = aioros.Error,
                Stopping = app.Lifetime.ApplicationStopping,
                Scheme = aioros.Scheme,
                Installed = AiorosMode.IsInstalled,
                Updates = AiorosMode.UpdateFeed(app.Configuration[UpdatesSetting], mode.Server),
                FirstRun = mode.FirstRun,
                ProjectDirectory = aioros.ProjectDirectory,
            },
            mode.Arguments);
    }

    /// <summary>The configuration key of the installed app's Velopack feed, a URL or a folder; the server's <c>/updates/</c> by default.</summary>
    public const string UpdatesSetting = "Aioros:Updates";

    /// <summary>Runs Velopack's hooks first in an installed app's start (an install, update or uninstall exits there); <see langword="true"/> on its first run.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool StartInstalled(string[] args, string? scheme) => Targets.Desktop.Handoff.AppUpdates.Start(args, scheme);

    /// <summary>Registers the engine, started as soon as the host first asks for it (warmup or precompilation at startup), the XAML sources it shares with hot reload, and hot reload itself in development.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddStarted(IServiceCollection services, AiorosApp app, bool development)
    {
        if (!Pages.PageEngines.AvaloniaIsSupported)
        {
            throw new NotSupportedException(Unsupported);
        }

        var sources = new XamlSources(app.Assembly, app.ProjectDirectory);
        services.AddSingleton(sources);
        services.AddSingleton(provider => DeferredPageEngine.Start(() => StartAsync(app, sources, Logger(provider))));
        if (development)
        {
            services.AddHostedService<Development.XamlWatcher>();
        }
    }

    /// <summary>
    /// Registers the engine for a precompilation: started by the first page it compiles, so the precompilation first loads
    /// every page's models as production does, before Avalonia starts (<see cref="Targets.Ssr.Pages.ProductionModels"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddPrecompiling(IServiceCollection services, AiorosApp app)
    {
        if (!Pages.PageEngines.AvaloniaIsSupported)
        {
            throw new NotSupportedException(Unsupported);
        }

        var sources = new XamlSources(app.Assembly, app.ProjectDirectory);
        services.AddSingleton(sources);
        services.AddSingleton(provider => new DeferredPageEngine(() => StartAsync(app, sources, Logger(provider))));
    }

    /// <summary>Whether a thread holds an Avalonia dispatcher: it created an Avalonia object.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HoldsAvalonia(Thread thread) => AvaloniaHost.HasDispatcher(thread);

    /// <summary>Registers the engine started only when a page or a post needs it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddDeferred(IServiceCollection services, AiorosApp app) =>
        services.AddSingleton(provider => new DeferredPageEngine(() => StartAsync(app, null, Logger(provider))));

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ISlotFallback Fallback() => Pages.PageEngines.AvaloniaIsSupported ? AdapterSlotResolver.Fallback : throw new NotSupportedException(Unsupported);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<IPageEngine> StartAsync(AiorosApp app, XamlSources? sources, Microsoft.Extensions.Logging.ILogger logger) =>
        Pages.PageEngines.AvaloniaIsSupported ? StartedAsync(app, sources, logger) : Task.FromException<IPageEngine>(new NotSupportedException(Unsupported));

    private static async Task<IPageEngine> StartedAsync(AiorosApp app, XamlSources? sources, Microsoft.Extensions.Logging.ILogger logger) =>
        new AvaloniaPageEngine(await AvaloniaHost.StartAsync(app.ApplicationType?.Invoke() ?? typeof(DefaultApplication), web: true, logger), sources ?? new XamlSources(app.Assembly, app.ProjectDirectory));

    private static Microsoft.Extensions.Logging.ILogger Logger(IServiceProvider services) =>
        services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("Aioros.Pages");
}

/// <summary>
/// The fallback of the runtime's slot resolver: the adapter's, loaded the first time a plan reads something no generated
/// code covers, so a production app whose plans the generated code covers never loads it.
/// </summary>
internal sealed class EngineFallback : ISlotFallback
{
    private ISlotFallback? _adapter;

    public static EngineFallback Instance { get; } = new();

    private ISlotFallback Adapter => Volatile.Read(ref _adapter) ?? Load();

    public Func<object, object?> Getter(Type owner, string member) => Adapter.Getter(owner, member);

    public ValueGetter Getter(SlotSpec spec, MemberPaths paths) => Adapter.Getter(spec, paths);

    public (Type ValueType, Action<object, object?> Set) Setter(Type owner, string member) => Adapter.Setter(owner, member);

    public ValueFormatter Formatter(SlotSpec spec, MemberPaths paths) => Adapter.Formatter(spec, paths);

    public string Format(SlotSpec spec, object value) => Adapter.Format(spec, value);

    public Func<System.Windows.Input.ICommand, object?, ValueTask> Command(Type type) => Adapter.Command(type);

    public Type Type(string name) => Adapter.Type(name);

    public FieldBinding? Binding(SlotSpec field, MemberPaths paths) => Adapter.Binding(field, paths);

    private ISlotFallback Load()
    {
        var adapter = AvaloniaEngine.Fallback();
        Volatile.Write(ref _adapter, adapter);
        return adapter;
    }
}
