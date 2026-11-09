using Aioros.Hosting;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Autofac.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopContainerTests(DesktopFixture fixture)
{
    private const string Layout = "Pages/Layout.axaml";
    private const string About = "Pages/about/Page.axaml";

    [Fact]
    [Trait("AC", "FUNCTIONS-10")]
    public void AppExtensionsNeedNoUsing()
    {
        var declared = new[] { typeof(AiorosFunctionRegistration), typeof(AiorosEndpoints) }.Select(type => type.Namespace).Distinct();

        Assert.Equal([typeof(WebApplicationBuilder).Namespace], declared);
    }

    [Fact]
    [Trait("AC", "DESKTOP-31")]
    public async Task TheDesktopTakesTheAppsContainerAsync()
    {
        var loaded = new List<Lease>();
        var holders = new List<Holder>();
        PageDefinition[] pages =
        [
            new("/leased", About, Unreachable, [new LayoutDefinition(Layout, static _ => null)])
            {
                Loader = new LoaderDefinition(
                    (services, _, _) =>
                    {
                        loaded.Add(services.GetRequiredService<Lease>());
                        return ValueTask.FromResult(new LoaderOutcome(null));
                    },
                    (services, _) => Add(holders, new Holder(services.GetRequiredService<Lease>()))),
            },
            new("/about", About, static _ => null, [new LayoutDefinition(Layout, static _ => null)]),
        ];
        var builder = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App(pages));
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Services.AddScoped<Lease>();
        await using var app = builder.Build();

        var (shown, left) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: app.Services, pages: pages);
            await navigator.NavigateAsync("/leased");
            var whileShown = holders[0].Lease.Disposed;
            await navigator.NavigateAsync("/about");
            var result = (whileShown, holders[0].Lease.Disposed);
            window.Close();
            return result;
        });

        Assert.IsType<AutofacServiceProvider>(app.Services);
        Assert.NotSame(loaded[0], holders[0].Lease);
        Assert.True(loaded[0].Disposed);
        Assert.False(shown);
        Assert.True(left);
    }

    [Fact]
    [Trait("AC", "DESKTOP-32")]
    public async Task AScopedMessengerIsSharedWithinAPageOnlyAsync()
    {
        var listeners = new List<Listener>();
        var senders = new List<Sender>();
        PageDefinition[] pages =
        [
            new("/inbox", About, services => Add(senders, new Sender(services.GetRequiredService<IMessenger>())), [
                new LayoutDefinition(Layout, services => Add(listeners, new Listener(services.GetRequiredService<IMessenger>()))),
            ]),
        ];
        await using var services = new ServiceCollection()
            .AddPageNavigator()
            .AddScoped<IMessenger, WeakReferenceMessenger>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: pages);
            await navigator.NavigateAsync("/inbox");
            await navigator.NavigateAsync("/inbox");
            window.Close();
            return true;
        });

        Assert.Same(listeners[0].Messenger, senders[0].Messenger);
        Assert.Same(listeners[1].Messenger, senders[1].Messenger);
        Assert.NotSame(listeners[0].Messenger, listeners[1].Messenger);
        Assert.Equal([1, 1], listeners.Select(static listener => listener.Received));
    }

    [Fact]
    [Trait("AC", "DESKTOP-33")]
    public void DevelopmentRejectsASingletonViewModelThatCapturesTheNavigator()
    {
        var captive = Builder(Environments.Development);
        captive.Services.AddSingleton<Captive>();
        using var development = Builder(Environments.Development).Build();
        var production = Builder(Environments.Production);
        production.Services.AddSingleton<Captive>();

        var failure = Assert.Throws<AggregateException>(() => captive.Build());
        var root = Assert.Throws<InvalidOperationException>(() => development.Services.GetService<INavigator>());
        using var built = production.Build();

        Assert.Contains($"Cannot consume scoped service '{typeof(INavigator).FullName}' from singleton '{typeof(Captive).FullName}'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("from root provider", root.Message, StringComparison.Ordinal);
        Assert.NotNull(built.Services.GetRequiredService<Captive>().Navigator);
    }

    [Fact]
    [Trait("AC", "DESKTOP-37")]
    public async Task PageScopesAreDisposedAsynchronouslyAsync()
    {
        var held = new List<Exclusive>();
        PageDefinition[] pages =
        [
            new("/held", About, services => Add(held, services.GetRequiredService<Exclusive>()), [new LayoutDefinition(Layout, static _ => null)]),
            new("/held-away", About, services => new Away(Add(held, services.GetRequiredService<Exclusive>())), [new LayoutDefinition(Layout, static _ => null)]),
            new("/about", About, static _ => null, [new LayoutDefinition(Layout, static _ => null)]),
        ];
        await using var services = new ServiceCollection()
            .AddPageNavigator()
            .AddScoped<Exclusive>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var (whileShown, current) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: pages);
            await navigator.NavigateAsync("/held");
            var shown = held[0].Disposed;
            await navigator.NavigateAsync("/about");
            await navigator.NavigateAsync("/held-away");
            var result = (shown, navigator.Current);
            window.Close();
            return result;
        });

        Assert.False(whileShown);
        Assert.Equal("/about", current);
        Assert.All(held, static exclusive => Assert.True(exclusive.Disposed));
        Assert.Equal(2, held.Count);
    }

    [Fact]
    [Trait("AC", "DESKTOP-38")]
    public async Task AKeptLayoutKeepsTheScopeThatCreatedItAsync()
    {
        var layouts = new List<Holder>();
        var holders = new List<Holder>();
        var leases = new LayoutDefinition("Pages/leases/Layout.axaml", Unreachable)
        {
            Loader = new LoaderDefinition(static (_, _, _) => ValueTask.FromResult(new LoaderOutcome(null)), (services, _) => Add(layouts, new Holder(services.GetRequiredService<Lease>())))
            {
                Receive = static (_, _) => { },
            },
        };
        PageDefinition[] pages =
        [
            new("/one", About, services => Add(holders, new Holder(services.GetRequiredService<Lease>())), [new LayoutDefinition(Layout, static _ => null), leases]),
            new("/two", About, services => Add(holders, new Holder(services.GetRequiredService<Lease>())), [new LayoutDefinition(Layout, static _ => null), leases]),
            new("/about", About, static _ => null, [new LayoutDefinition(Layout, static _ => null)]),
        ];
        await using var services = new ServiceCollection()
            .AddPageNavigator()
            .AddScoped<Lease>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var (inside, outside) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: pages);
            await navigator.NavigateAsync("/one");
            await navigator.NavigateAsync("/two");
            await navigator.NavigateAsync("/one");
            var whileInside = holders.Select(static holder => holder.Lease.Disposed).Prepend(layouts[0].Lease.Disposed).ToList();
            await navigator.NavigateAsync("/about");
            var result = (whileInside, holders.Select(static holder => holder.Lease.Disposed).Prepend(layouts[0].Lease.Disposed).ToList());
            window.Close();
            return result;
        });

        Assert.Single(layouts);
        Assert.Same(layouts[0].Lease, holders[0].Lease);
        Assert.Equal(3, holders.Select(static holder => holder.Lease).Distinct().Count());
        Assert.Equal([false, false, true, false], inside);
        Assert.Equal([true, true, true, true], outside);
    }

    [Fact]
    [Trait("AC", "DESKTOP-39")]
    public void TheDesktopFlagIsASwitch()
    {
        var builder = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag, "--environment=Development"], App([]));
        using var app = builder.Build();

        Assert.Equal(Environments.Development, builder.Environment.EnvironmentName);
        Assert.True(app.Services.GetRequiredService<AiorosMode>().Desktop);
    }

    private static WebApplicationBuilder Builder(string environment) =>
        AiorosHost.CreateBuilder(["--environment=" + environment, AiorosMode.DesktopFlag], App([]));

    private static AiorosApp App(PageDefinition[] pages) =>
        new([.. pages], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);

    private static T Add<T>(List<T> list, T item)
    {
        list.Add(item);
        return item;
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    internal sealed record Holder(Lease Lease);

    internal sealed class Away(Exclusive exclusive) : IPageModel
    {
        public Exclusive Exclusive { get; } = exclusive;

        public string? RedirectTo => "/about";
    }

    internal sealed record Ping(string From);

    internal sealed class Listener
    {
        public Listener(IMessenger messenger)
        {
            Messenger = messenger;
            messenger.Register<Listener, Ping>(this, static (listener, _) => listener.Received++);
        }

        public IMessenger Messenger { get; }

        public int Received { get; private set; }
    }

    internal sealed class Sender
    {
        public Sender(IMessenger messenger)
        {
            Messenger = messenger;
            messenger.Send(new Ping(nameof(Sender)));
        }

        public IMessenger Messenger { get; }
    }

    internal sealed class Captive(INavigator navigator)
    {
        public INavigator Navigator { get; } = navigator;
    }
}
