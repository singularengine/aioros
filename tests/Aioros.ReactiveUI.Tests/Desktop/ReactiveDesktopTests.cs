using Aioros.Avalonia.Hosting;
using Aioros.Pages;
using Aioros.ReactiveUI.Tests.Models;
using Aioros.Runtime.Pages;
using Aioros.Targets.Desktop.Navigation;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.ReactiveUI.Tests.Desktop;

public sealed class ReactiveDesktopTests(DesktopFixture fixture)
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "REACTIVE-04")]
    public async Task CommandsMoveTheDesktopPageStackAsync()
    {
        var activations = new Activations();
        var collection = new ServiceCollection().AddPageNavigator().AddSingleton(activations).AddTransient<ShelfViewModel>();
        collection.AddReactiveRoutes(routes => routes.Map<AlbumViewModel>(album => $"/albums/{album.Id}"));
        await using var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        PageDefinition[] pages =
        [
            new("/shelf", "Pages/shelf/Page.axaml", provider => provider.GetRequiredService<ShelfViewModel>(), []),
            new("/albums/{id}", "Pages/albums/Page.axaml", _ => null, []),
        ];

        var (shown, current, texts) = await fixture.OnUiAsync(async () =>
        {
            var window = new Window { Width = 800, Height = 600 };
            var navigator = new DesktopNavigator(window, new XamlSources(typeof(ReactiveDesktopTests).Assembly), services, pages, null);
            window.Show();
            await navigator.NavigateAsync("/shelf", TestContext.Current.CancellationToken);
            var whileShown = (activations.Started, activations.Ended);
            var navigated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigator.Navigated += (_, path) => navigated.TrySetResult(path);
            DesktopFixture.Click(window, "Open");
            await navigated.Task.WaitAsync(TestContext.Current.CancellationToken);
            var result = (whileShown, navigator.Current, DesktopFixture.Texts(window));
            window.Close();
            return result;
        });

        Assert.Equal((1, 0), shown);
        Assert.Equal("/albums/7", current);
        Assert.Equal(["Album page"], texts);
        Assert.Equal((1, 1), (activations.Started, activations.Ended));
    }
}
