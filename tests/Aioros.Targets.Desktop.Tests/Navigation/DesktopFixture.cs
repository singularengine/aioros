using Aioros.Avalonia.Hosting;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Microsoft.Extensions.DependencyInjection;
using Aioros.Targets.Desktop.Navigation;
using Aioros.Targets.Desktop.Tests.Navigation;
using Aioros.Tests.SignIn;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;

[assembly: AssemblyFixture(typeof(DesktopFixture))]

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public sealed class DesktopFixture : IAsyncLifetime
{
    private const string Layout = "Pages/Layout.axaml";

    public AvaloniaHost Host { get; private set; } = null!;

    public static PageDefinition[] Pages { get; } =
    [
        Page("/", "Pages/Page.axaml", _ => new Home()),
        Page("/items/{id}", "Pages/items/[id]/Page.axaml", _ => new Item()),
        Page("/about", "Pages/about/Page.axaml", _ => null),
        Page("/inline", "Pages/inline/Page.axaml", _ => null),
        Page("/form", "Pages/form/Page.axaml", _ => new Form()),
        Page("/later", "Pages/later/Page.axaml", _ => new Later()),
        Page("/menu", "Pages/menu/Page.axaml", _ => new Later()),
        Page("/gone", "Pages/gone/Page.axaml", _ => new Gone()),
        Page("/old", "Pages/old/Page.axaml", _ => new Old()),
        Page("/broken", "Pages/broken/Page.axaml", _ => new Broken()),
        Page("/leased", "Pages/about/Page.axaml", Lend),
        Section("/sections/one", "Pages/sections/one/Page.axaml", _ => null),
        Section("/sections/pick", "Pages/sections/pick/Page.axaml", _ => new Pick()),
        Page("/leave", "Pages/leave/Page.axaml", services => new Leaving(services.GetRequiredService<INavigator>())),
        Page("/entering", "Pages/old/Page.axaml", services => new Entering(services.GetRequiredService<INavigator>())),
        Page("/arriving", "Pages/old/Page.axaml", services => new Arriving(services.GetRequiredService<INavigator>())),
        Loaded("/sections/loaded/{id}", "Pages/items/[id]/Page.axaml", Loader(
            (_, request, _) => OutcomeAsync(new ItemData("loaded " + request.Value("id") + "/" + request.Query.GetValueOrDefault("tab"))),
            data => new Item((ItemData)data!))),
        Loaded("/sections/also", "Pages/sections/one/Page.axaml", null),
        Page("/failing", "Pages/about/Page.axaml", Unreachable) with
        {
            Loader = Loader((_, _, _) => ValueTask.FromException<LoaderOutcome>(new InvalidOperationException("The loader is down.")), _ => new Detour()),
        },
        Page("/detour", "Pages/about/Page.axaml", Unreachable) with
        {
            Loader = Loader(
                async (services, _, cancellationToken) =>
                {
                    await services.GetRequiredService<INavigator>().NavigateAsync(new NavigationRequest("/items/9"), cancellationToken);
                    return default;
                },
                _ => new Detour()),
        },
        Page("/missing", "Pages/about/Page.axaml", Unreachable) with
        {
            Loader = Loader((_, _, _) => ValueTask.FromResult(LoaderResult.NotFound()), _ => new Detour()),
        },
        Page("/moved/here", "Pages/about/Page.axaml", Unreachable) with
        {
            Loader = Loader((_, _, _) => ValueTask.FromResult(LoaderResult.Redirect("../items/5")), _ => new Detour()),
        },
        Loaded("/sections/titled", "Pages/sections/one/Page.axaml", Loader((_, _, _) => ValueTask.FromResult(new LoaderOutcome(null) { Title = "Titled · Lumen" }), _ => new Detour())),
        Loaded("/sections/untitled", "Pages/sections/one/Page.axaml", Loader((_, _, _) => OutcomeAsync(null), _ => new Detour())),
        Headed("/sections/static", null),
        Headed("/sections/overridden", Loader((_, _, _) => ValueTask.FromResult(new LoaderOutcome { Title = "Loaded" }), _ => new Detour())),
    ];

    public static ServiceProvider Navigation() =>
        new ServiceCollection()
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(services => services.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    public static PageDefinition NotFound { get; } = Page("/_not-found", "Pages/NotFound.axaml", _ => null);

    public static List<Lease> Leases { get; } = [];

    private static object? Lend(IServiceProvider services)
    {
        var lease = (Lease)services.GetService(typeof(Lease))!;
        Leases.Add(lease);
        return null;
    }

    public static PageDefinition Error { get; } = Page("/_error", "Pages/Error.axaml", _ => null);

    public async ValueTask InitializeAsync()
    {
        Host = await AvaloniaHost.StartAsync(typeof(TestApp));
        await AuthorizationServer.WarmUpAsync();
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    public Task<T> OnUiAsync<T>(Func<Task<T>> action) => Host.InvokeAsync(action, TestContext.Current.CancellationToken);

    public static (Window Window, DesktopNavigator Navigator) Open(bool withNotFound = true, bool withError = true, IServiceProvider? services = null, PageDefinition[]? pages = null, PageDefinition? notFound = null, PageDefinition? error = null, XamlSources? sources = null)
    {
        var window = new Window { Width = 800, Height = 600 };
        var navigator = new DesktopNavigator(window, sources ?? new XamlSources(typeof(DesktopFixture).Assembly), services ?? new Services(), pages ?? Pages, withNotFound ? notFound ?? NotFound : null, withError ? error ?? Error : null);
        window.Show();
        return (window, navigator);
    }

    public static IEnumerable<string> Texts(Window window)
    {
        Settle(window);
        return window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).Where(text => text.Length > 0).ToList();
    }

    public static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The next <see cref="DesktopNavigator.Navigated"/>, bounded by the test's own timeout so a regression fails instead of hanging.</summary>
    public static Task<string> NextNavigationAsync(DesktopNavigator navigator, CancellationToken bound)
    {
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, string path)
        {
            navigator.Navigated -= Handler;
            done.TrySetResult(path);
        }

        navigator.Navigated += Handler;
        return done.Task.WaitAsync(bound);
    }

    public static void Click(Window window, string text)
    {
        Settle(window);
        var target = window.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    public static PageDefinition Page(string route, string source, Func<IServiceProvider, object?> model) =>
        new(route, source, model, [new LayoutDefinition(Layout, _ => null)]);

    private static PageDefinition Loaded(string route, string source, LoaderDefinition? loader) =>
        new(route, source, loader is null ? _ => null : Unreachable, [
            new LayoutDefinition(Layout, _ => null),
            new LayoutDefinition("Pages/sections/Layout.axaml", Unreachable)
            {
                Loader = Loader((_, request, _) => ValueTask.FromResult(new LoaderOutcome(new SectionData("loaded " + request.Path)) { Title = "Section" }), data => new Section((SectionData)data!)),
            },
        ])
        {
            Loader = loader,
        };

    private static PageDefinition Headed(string route, LoaderDefinition? loader) =>
        new(route, "Pages/sections/one/Page.axaml", _ => null, [
            new LayoutDefinition(Layout, _ => null) { Head = new PageHead("Lumen", "%s · Lumen") },
            new LayoutDefinition("Pages/sections/Layout.axaml", _ => new Section()),
        ])
        {
            Head = new PageHead("Static"),
            Loader = loader,
        };

    private static LoaderDefinition Loader(Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<LoaderOutcome>> load, Func<object?, object> create) =>
        new(load, (_, data) => create(data));

    private static ValueTask<LoaderOutcome> OutcomeAsync(object? data) => ValueTask.FromResult(new LoaderOutcome(data));

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    private static PageDefinition Section(string route, string source, Func<IServiceProvider, object?> model) =>
        new(route, source, model, [new LayoutDefinition(Layout, _ => null), new LayoutDefinition("Pages/sections/Layout.axaml", _ => new Section())]);

    private sealed class Services : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
