using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Functions;
using Aioros.Targets.Ssr.Hosting;
using Autofac.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ContainerTests
{
    [Fact]
    [Trait("AC", "SSR-62")]
    public async Task AnotherContainerServesPagesPostsAndFunctionsAsync()
    {
        var tickets = new ConcurrentBag<Ticket>();
        var loads = 0;
        var page = new PageDefinition("/issues/{id}", "Pages/issues/[id]/Page.axaml", Unreachable, [])
        {
            Loader = new LoaderDefinition(
                (services, request, _) =>
                {
                    tickets.Add(services.GetRequiredService<Ticket>());
                    return ValueTask.FromResult(new LoaderOutcome($"issue {request.Value("id")} #{Interlocked.Increment(ref loads)}"));
                },
                static (_, data) => new Model((string)data!)),
        };
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.WebHost.UseTestServer();
        builder.Services.AddAiorosPages(new SsrOptions())
            .AddSingleton<IPageEngine>(new FakeEngine { Command = static _ => ValueTask.CompletedTask })
            .AddScoped<Ticket>()
            .AddLogging()
            .AddServerFunction<ICounter, Counter>([FunctionTests.Group], server: null);
        await using var app = builder.Build();
        app.MapServerFunctions([FunctionTests.Group]);
        app.MapAiorosPages([page]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var read = await client.GetAsync(new Uri("/issues/7", UriKind.Relative), TestContext.Current.CancellationToken);
        var shown = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var form = new StringContent("_btn=1%7Cx", Encoding.UTF8, "application/x-www-form-urlencoded");
        using var posted = await client.PostAsync(new Uri("/issues/7", UriKind.Relative), form, TestContext.Current.CancellationToken);
        var revalidated = await posted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var call = new StringContent("""{"amount":2}""", Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        call.Headers.Add(ServerFunctionGroup.FunctionHeader, "1");
        call.Headers.Add(ServerFunctionGroup.ContractHeader, "c1");
        using var called = await client.PostAsync(new Uri("/_sf/counter/add", UriKind.Relative), call, TestContext.Current.CancellationToken);

        Assert.IsType<AutofacServiceProvider>(app.Services);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Contains("<p>issue 7 #1</p>", shown, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Contains("<p>issue 7 #3</p>", revalidated, StringComparison.Ordinal);
        Assert.Equal(3, tickets.Select(static ticket => ticket.Id).Distinct().Count());
        Assert.Equal((HttpStatusCode.OK, "42"), (called.StatusCode, await called.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    [Trait("AC", "SSR-63")]
    public async Task AScopedMessengerIsSharedWithinARequestOnlyAsync()
    {
        var listeners = new ConcurrentQueue<Listener>();
        var senders = new ConcurrentQueue<Sender>();
        var page = new PageDefinition("/inbox", "Pages/inbox/Page.axaml", services => Enqueue(senders, new Sender(services.GetRequiredService<IMessenger>())), [
            new LayoutDefinition("Pages/Layout.axaml", services => Enqueue(listeners, new Listener(services.GetRequiredService<IMessenger>()))),
        ]);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAiorosPages(new SsrOptions())
            .AddSingleton<IPageEngine>(new FakeEngine())
            .AddScoped<IMessenger, WeakReferenceMessenger>();
        await using var app = builder.Build();
        app.MapAiorosPages([page]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var first = await client.GetAsync(new Uri("/inbox", UriKind.Relative), TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(new Uri("/inbox", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        var (layouts, pages) = (listeners.ToArray(), senders.ToArray());
        Assert.Same(layouts[0].Messenger, pages[0].Messenger);
        Assert.Same(layouts[1].Messenger, pages[1].Messenger);
        Assert.NotSame(layouts[0].Messenger, layouts[1].Messenger);
        Assert.Equal([1, 1], layouts.Select(static listener => listener.Received));
    }

    [Fact]
    [Trait("AC", "SSR-64")]
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

    private static WebApplicationBuilder Builder(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Services.AddAiorosPages(new SsrOptions()).AddSingleton<IPageEngine>(new FakeEngine());
        return builder;
    }

    private static T Enqueue<T>(ConcurrentQueue<T> queue, T item)
    {
        queue.Enqueue(item);
        return item;
    }

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");

    internal sealed class Ticket
    {
        public Guid Id { get; } = Guid.NewGuid();
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
