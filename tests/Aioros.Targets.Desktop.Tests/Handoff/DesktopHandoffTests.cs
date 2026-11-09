using System.IO.Compression;
using System.Security.Cryptography;
using Aioros.Functions;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Desktop.Handoff;
using Aioros.Targets.Desktop.Tests.Navigation;
using Aioros.Targets.Ssr.Data;
using Aioros.Tests.SignIn;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aioros.Targets.Ssr.Updates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Locators;

namespace Aioros.Targets.Desktop.Tests.Handoff;

public sealed class DesktopHandoffTests(DesktopFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "INSTALL-05")]
    public async Task AForwardedDeepLinkOpensItsPageAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var name = "aioros-test-" + Guid.NewGuid().ToString("N");
        var routes = DeepLink.Routes(DesktopFixture.Pages);

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open();
            await navigator.NavigateAsync("/");
            await using var running = SingleInstance.Claim(name, [])!;
            using var stop = new CancellationTokenSource();
            var opened = new List<string?>();
            var arrivals = new List<TaskCompletionSource>();
            for (var i = 0; i < 3; i++)
            {
                arrivals.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }

            var following = DeepLink.FollowAsync(running.Forwarded, "shop", routes, async path =>
            {
                if (path is not null)
                {
                    await navigator.TryNavigateAsync(path);
                }

                arrivals[opened.Count].SetResult();
                opened.Add(path);
            }, stop.Token);

            SingleInstance.Claim(name, ["shop://open?path=%2Fnowhere"]);
            await arrivals[0].Task.WaitAsync(bound);
            var rejected = navigator.Current;
            SingleInstance.Claim(name, ["--desktop"]);
            await arrivals[1].Task.WaitAsync(bound);
            var plain = navigator.Current;
            SingleInstance.Claim(name, ["shop://open?path=%2Fitems%2F7"]);
            await arrivals[2].Task.WaitAsync(bound);
            var result = (rejected, plain, navigator.Current, opened);
            await stop.CancelAsync();
            await following;
            window.Close();
            return result;
        });

        Assert.Equal(("/", "/", "/items/7"), (outcome.rejected, outcome.plain, outcome.Current));
        Assert.Equal([null, null, "/items/7"], outcome.opened);
    }

    [Fact]
    [Trait("AC", "INSTALL-11")]
    public async Task OpenInAppLinksAreHiddenOnTheDesktopAsync()
    {
        PageDefinition[] pages = [new("/handoff", "Pages/handoff/Page.axaml", static _ => null, [])];

        var visible = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: pages);
            await navigator.NavigateAsync("/handoff");
            DesktopFixture.Settle(window);
            var links = window.GetVisualDescendants().OfType<HyperlinkButton>()
                .ToDictionary(link => link.Content?.ToString() ?? string.Empty, link => link.IsVisible);
            window.Close();
            return links;
        });

        Assert.Equal(new Dictionary<string, bool> { ["Open in the desktop app"] = false, ["Download"] = true }, visible);
    }

    [Fact]
    [Trait("AC", "INSTALL-12")]
    public async Task AnOutdatedClientUpdatesAndRestartsOnThePathAsync()
    {
        PageDefinition[] pages = [.. DesktopFixture.Pages, Outdated];

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: pages);
            var failures = new List<Exception>();
            navigator.Failed += (_, exception) => failures.Add(exception);
            var asked = new List<string>();
            await navigator.NavigateAsync("/about");
            navigator.Outdated = path =>
            {
                asked.Add(path);
                return Task.FromResult(true);
            };
            await navigator.TryNavigateAsync("/outdated?tab=2");
            var restarting = (failures.Count, Texts: DesktopFixture.Texts(window).ToList());
            navigator.Outdated = path =>
            {
                asked.Add(path);
                return Task.FromResult(false);
            };
            await navigator.TryNavigateAsync("/outdated");
            var noUpdate = (failures.Count, Texts: DesktopFixture.Texts(window).ToList());
            navigator.Outdated = null;
            await navigator.NavigateAsync("/about");
            await navigator.TryNavigateAsync("/outdated");
            var without = (failures.Count, Texts: DesktopFixture.Texts(window).ToList());
            window.Close();
            return (asked, restarting, noUpdate, without, failures);
        });

        Assert.Equal(["/outdated?tab=2", "/outdated"], outcome.asked);
        Assert.Equal(0, outcome.restarting.Count);
        Assert.Equal(["Shell", "About page"], outcome.restarting.Texts);
        Assert.Equal(1, outcome.noUpdate.Count);
        Assert.Contains("Something went wrong", outcome.noUpdate.Texts);
        Assert.Equal(2, outcome.without.Count);
        Assert.Contains("Something went wrong", outcome.without.Texts);
        Assert.All(outcome.failures, failure => Assert.IsType<ClientOutdatedException>(failure));
        Assert.Equal(
            ["--desktop", "--Aioros:Server=https://shop.example", "--path=/outdated?tab=2"],
            AppUpdates.RestartArguments(["--desktop", "--path=/", "shop://open?path=%2Forders", "--Aioros:Server=https://shop.example"], "/outdated?tab=2"));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "INSTALL-14")]
    public async Task ADeepLinkToAProtectedPageSignsInAndLandsThereAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var name = "aioros-test-" + Guid.NewGuid().ToString("N");
        await using var server = await AuthorizationServer.StartAsync(map: static app => app.MapPageData([Mine]));
        var cookie = await server.CookieAsync();
        var store = new SessionTokenStore();
        var signIn = new SignInClient(server.Options, server.Client(), store, TimeProvider.System);
        await using var services = new ServiceCollection()
            .AddSingleton(signIn)
            .AddSingleton<ILoaderResults>(new RemoteLoaderResults(server.Client(new SignedInHandler(signIn))))
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider();
        var browser = server.Browser(cookie);

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services, pages: [Mine]);
            var opened = new List<string>();
            navigator.Browser = async address =>
            {
                opened.Add(address.AbsolutePath);
                await browser(address);
            };
            await using var running = SingleInstance.Claim(name, [])!;
            using var stop = new CancellationTokenSource();
            var landed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var following = DeepLink.FollowAsync(running.Forwarded, "shop", DeepLink.Routes([Mine]), async path =>
            {
                await navigator.TryNavigateAsync(path!);
                landed.SetResult();
            }, stop.Token);

            SingleInstance.Claim(name, ["shop://open?path=%2Fmine"]);
            await landed.Task.WaitAsync(bound);
            var result = (navigator.Current, Texts: DesktopFixture.Texts(window).ToList(), opened);
            await stop.CancelAsync();
            await following;
            window.Close();
            return result;
        });

        Assert.Equal(["/connect/authorize"], outcome.opened);
        Assert.Equal("/mine", outcome.Current);
        Assert.Equal(["mine of ana"], outcome.Texts);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "INSTALL-17")]
    public async Task TheFirstRunSignsInAndLandsOnThePageTheUserCameFromAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(pages: static _ => true);
        var cookie = await server.CookieAsync();
        var store = new SessionTokenStore();
        var signIn = new SignInClient(server.Options, server.Client(), store, TimeProvider.System);
        await using var services = new ServiceCollection()
            .AddSingleton(signIn)
            .AddScoped<PageNavigator>()
            .AddScoped<INavigator>(static provider => provider.GetRequiredService<PageNavigator>())
            .BuildServiceProvider();
        var first = FirstRun.Path("/", firstRun: true, server.Options);

        var outcome = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: services);
            var landed = new List<string?>();
            foreach (var held in new[] { "; .Aioros.Return=%2Fitems%2F7", string.Empty, "; .Aioros.Return=%2Fnowhere" })
            {
                navigator.Browser = server.Browser(cookie + held);
                await navigator.TryNavigateAsync(first);
                landed.Add(navigator.Current);
            }

            window.Close();
            return landed;
        });

        Assert.Equal("/login?ReturnUrl=%2F&FirstRun=true", first);
        Assert.Equal("/", FirstRun.Path("/", firstRun: false, server.Options));
        Assert.Equal("/orders", FirstRun.Path("/orders", firstRun: true, null));
        Assert.Equal(["/items/7", "/", "/"], outcome);
        Assert.NotNull(await store.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "INSTALL-19")]
    public async Task TheInstalledAppDownloadsItsUpdateFromTheSiteAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var feed = Directory.CreateTempSubdirectory("aioros-feed-").FullName;
        var packages = Directory.CreateTempSubdirectory("aioros-packages-").FullName;
        try
        {
            var package = Path.Combine(feed, "Shop-1.0.1-linux-full.nupkg");
            await using (var zip = await ZipFile.OpenAsync(package, ZipArchiveMode.Create, bound))
            {
                var nuspec = zip.CreateEntry("Shop.nuspec");
                await using var writer = new StreamWriter(await nuspec.OpenAsync(bound));
                await writer.WriteAsync("""<?xml version="1.0" encoding="utf-8"?><package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"><metadata><id>Shop</id><version>1.0.1</version><title>Shop</title><authors>Shop</authors><description>Shop</description></metadata></package>""");
            }

            var bytes = await File.ReadAllBytesAsync(package, bound);
            await File.WriteAllTextAsync(
                Path.Combine(feed, "releases.linux.json"),
                $$"""{"Assets":[{"PackageId":"Shop","Version":"1.0.1","Type":"Full","FileName":"Shop-1.0.1-linux-full.nupkg","SHA256":"{{Convert.ToHexString(SHA256.HashData(bytes))}}","Size":{{bytes.Length}}}]}""",
                bound);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var site = builder.Build();
            site.UseUpdateFeed(feed);
            await site.StartAsync(bound);
            var address = site.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

            var manager = new UpdateManager(address + "/updates/", new UpdateOptions { ExplicitChannel = "linux" }, new TestVelopackLocator("Shop", "1.0.0", packages, null));
            var update = await manager.CheckForUpdatesAsync();
            await manager.DownloadUpdatesAsync(update!, null, bound);

            Assert.Equal("1.0.1", update!.TargetFullRelease.Version.ToString());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(packages, "Shop-1.0.1-linux-full.nupkg"), bound));
        }
        finally
        {
            Directory.Delete(feed, recursive: true);
            Directory.Delete(packages, recursive: true);
        }
    }

    private static readonly PageDefinition Outdated = new("/outdated", "Pages/about/Page.axaml", static _ => throw new InvalidOperationException("A view model with a loader is created from its data."), [new LayoutDefinition("Pages/Layout.axaml", static _ => null)])
    {
        Loader = new LoaderDefinition(
            static (_, _, _) => ValueTask.FromException<LoaderOutcome>(new ClientOutdatedException("The server sends this page's data in a newer shape; update the app.", "c2")),
            static (_, _) => new object()),
    };

    private static readonly PageDefinition Mine = new("/mine", "Pages/items/[id]/Page.axaml", static _ => throw new InvalidOperationException("A view model with a loader is created from its data."), [])
    {
        Access = [new PageAccess(null, null)],
        DataContract = "c1",
        Loader = new LoaderDefinition(
            static (_, request, _) => ValueTask.FromResult(new LoaderOutcome(new ItemData("mine of " + request.User.Identity?.Name))),
            static (_, data) => new Item((ItemData)data!))
        {
            Write = static (writer, data) => writer.WriteStringValue(((ItemData)data!).Label),
            Read = static element => new ItemData(element.GetString()!),
        },
    };
}
