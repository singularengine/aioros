using Aioros.Hosting;
using Aioros.Pages;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopAppNavigationTests(DesktopFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-40")]
    public async Task CommandsNavigateThroughTheAppsOwnInterfaceAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        PageDefinition[] pages =
        [
            new("/sending", "Pages/sending/Page.axaml", services => new Sending(services.GetRequiredService<IShellNavigator>()), [new LayoutDefinition("Pages/Layout.axaml", static _ => null)]),
            new("/about", "Pages/about/Page.axaml", static _ => null, [new LayoutDefinition("Pages/Layout.axaml", static _ => null)]),
        ];
        var builder = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App(pages, Aioros.Generated.AiorosNavigators.All));
        builder.AddNavigation<IShellNavigator>();
        await using var app = builder.Build();
        var unknown = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App(pages, []));

        var (path, texts) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(services: app.Services, pages: pages);
            await navigator.NavigateAsync("/sending");
            var navigated = DesktopFixture.NextNavigationAsync(navigator, bound);
            DesktopFixture.Click(window, "Send");
            var result = (await navigated, DesktopFixture.Texts(window).ToList());
            window.Close();
            return result;
        });
        var failure = Assert.Throws<InvalidOperationException>(() => unknown.AddNavigation<IShellNavigator>());

        Assert.Equal("/about", path);
        Assert.Equal(["Shell", "About page"], texts);
        Assert.Contains(nameof(IShellNavigator), failure.Message, StringComparison.Ordinal);
        Assert.Contains("AddNavigation in the app's project", failure.Message, StringComparison.Ordinal);
    }

    private static AiorosApp App(PageDefinition[] pages, System.Collections.Immutable.ImmutableArray<Runtime.Pages.NavigationInterface> navigators) =>
        new([.. pages], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory) { Navigators = navigators };
}

public interface IShellNavigator
{
    ValueTask GoToAsync(string path, CancellationToken cancellationToken);
}

public sealed partial class Sending(IShellNavigator navigator)
{
    [RelayCommand]
    private Task SendAsync() => navigator.GoToAsync("about", CancellationToken.None).AsTask();
}
