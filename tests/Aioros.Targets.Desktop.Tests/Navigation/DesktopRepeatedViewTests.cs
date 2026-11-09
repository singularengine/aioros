using System.Collections.Concurrent;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Targets.Desktop.Tests.Views;
using Aioros.Targets.Ssr.Data;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopRepeatedViewTests(DesktopFixture fixture)
{
    private const string Host = "Pages/deck/Page.axaml";
    private const string Key = Host + ":0.1.0.0.0";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-50")]
    public async Task AnItemsCommandRefreshesThatItemAloneAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var batch = new Batch("client");
        await using var services = DesktopFixture.Navigation();

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [Page(batch)]);
            await navigator.NavigateAsync("/deck");
            var (a, b) = (Item(window, 0), Item(window, 1));
            var shown = (a.Label, b.Label, string.Join("|", batch.Inputs.Select(inputs => string.Join(",", inputs))));
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopViewCommandTests.Press(window, "View save", 1);
            var after = (await settled, Item(window, 0) == a, Item(window, 1) == b, a.Label, b.Label, b.Saved, string.Join("|", batch.Inputs.Select(inputs => string.Join(",", inputs))));
            window.Close();
            return (shown, after);
        });

        Assert.Equal(("a 1 on client", "b 1 on client", "a,b"), result.shown);
        Assert.Equal((true, true, true, "a 1 on client", "b 2 on client", "Saved", "a,b|b"), result.after);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-51")]
    public async Task RemoteItemCommandsAskForThatItemAloneAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var server = new Batch("server");
        await using var app = await ServerAsync(server);
        var requests = new ConcurrentQueue<string>();
        var http = new HttpClient(new Recorder(app.GetTestServer().CreateHandler(), requests)) { BaseAddress = app.GetTestServer().BaseAddress };
        await using var services = new ServiceCollection()
            .AddSingleton<ILoaderResults>(new RemoteLoaderResults(http))
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [Page(new Batch("client"))]);
            await navigator.NavigateAsync("/deck");
            var (a, b) = (Item(window, 0), Item(window, 1));
            var shown = (a.Label, b.Label);
            var settled = DesktopViewCommandTests.NextSettledAsync(navigator, bound);
            DesktopViewCommandTests.Press(window, "View save", 1);
            var after = (await settled, Item(window, 0) == a, a.Label, Item(window, 1).Label);
            window.Close();
            return (shown, after);
        });

        Assert.Equal(("a 1 on server", "b 1 on server"), result.shown);
        Assert.Equal((true, true, "a 1 on server", "b 2 on server"), result.after);
        Assert.Equal(["/_data/deck c1", $"/_data/deck c1 {Key}[b]"], requests);
        Assert.Equal("a,b|b", string.Join("|", server.Inputs.Select(inputs => string.Join(",", inputs))));
    }

    private static Saving Item(Window window, int index)
    {
        DesktopFixture.Settle(window);
        return (Saving)window.GetVisualDescendants().OfType<Button>().Where(button => button.Content is "View save").ElementAt(index).DataContext!;
    }

    private static PageDefinition Page(Batch batch) =>
        new("/deck", Host, static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
        {
            Loader = new LoaderDefinition(static (_, _, _) => ValueTask.FromResult(new LoaderOutcome(new Deck("deck", [new Card("a"), new Card("b")]))), static (_, data) => data)
            {
                Write = static (writer, data) => writer.WriteStringValue(string.Join(",", ((Deck)data!).Cards.Select(static card => card.Id))),
                Read = static element => new Deck("deck", [.. element.GetString()!.Split(',').Select(static id => new Card(id))]),
            },
            Views =
            [
                new ViewInstance(Key, 1, new ViewDefinition("Views/Saver.axaml", static _ => null) { Type = static () => typeof(Saver), Loader = batch.Loader })
                {
                    List = new ViewList(0, static model => ((Deck)model!).Cards) { Key = static item => ((Card)item!).Id, Parse = static text => text },
                },
            ],
            DataContract = "c1",
        };

    private static async Task<WebApplication> ServerAsync(Batch batch)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<PageNavigator>().AddScoped<INavigator>(static services => services.GetRequiredService<PageNavigator>());
        var app = builder.Build();
        app.MapPageData([Page(batch)]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Batch(string origin)
    {
        private int _calls;

        public ConcurrentQueue<string[]> Inputs { get; } = new();

        public LoaderDefinition Loader => new(
            static (_, _, _) => throw new InvalidOperationException("A batch loader loads many."),
            static (_, data) => new Saving((ItemData)data!, null))
        {
            LoadManyAsync = (_, inputs, _, _) =>
            {
                Inputs.Enqueue([.. inputs.Cast<string>()]);
                var call = Interlocked.Increment(ref _calls);
                return ValueTask.FromResult<IReadOnlyList<LoaderOutcome>>([.. inputs.Cast<string>().Select(id => new LoaderOutcome(new ItemData($"{id} {call} on {origin}")))]);
            },
            Receive = static (model, data) => ((Saving)model).Receive((ItemData)data!),
            Write = static (writer, data) => writer.WriteStringValue(((ItemData)data!).Label),
            Read = static element => new ItemData(element.GetString()!),
        };
    }

    private sealed class Recorder(HttpMessageHandler inner, ConcurrentQueue<string> requests) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue(request.RequestUri!.PathAndQuery + " " + string.Join(",", request.Headers.GetValues("X-Aioros-Contract"))
                + (request.Headers.TryGetValues(RemoteLoaderResults.ViewHeader, out var view) ? " " + view.Single() : string.Empty));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
