using Aioros.Pages;
using Aioros.Targets.Desktop.Tests.Views;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopViewTests(DesktopFixture fixture)
{
    [Fact]
    [Trait("AC", "DESKTOP-45")]
    public async Task ViewsAreComposedWithTheirOwnModelsAsync()
    {
        const string Host = "Pages/composed/Page.axaml";
        var page = new PageDefinition("/composed", Host, Unreachable, [new LayoutDefinition("Pages/Layout.axaml", _ => null)])
        {
            Loader = Loader(_ => new LoaderOutcome(new ItemData("page")) { Title = "Composed page" }),
            Views =
            [
                new ViewInstance(Host + ":0.1", 1, new ViewDefinition("Views/Badge.axaml", Unreachable) { Type = static () => typeof(Badge), Loader = Loader(_ => new LoaderOutcome(new ItemData("badge")) { Title = "Badge title" }) }),
                new ViewInstance(Host + ":0.2", 1, new ViewDefinition("Pages/items/[id]/Page.axaml", Unreachable) { Loader = Loader(_ => new LoaderOutcome(new ItemData("embedded"))) }),
                new ViewInstance(Host + ":0.3", 1, new ViewDefinition("Views/Badge.axaml", Unreachable) { Type = static () => typeof(Badge), Loader = Loader(_ => throw new InvalidOperationException("The badge is down.")) }),
            ],
        };

        var (texts, title) = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page]);
            await navigator.NavigateAsync("/composed");
            var result = (DesktopFixture.Texts(window).ToList(), window.Title);
            window.Close();
            return result;
        });

        Assert.Equal(["Shell", "page", "badge", "embedded", "This part of the page could not be loaded."], texts);
        Assert.Equal("Composed page", title);
    }

    private static LoaderDefinition Loader(Func<PageRequest, LoaderOutcome> load) =>
        new((_, request, _) => ValueTask.FromResult(load(request)), (_, data) => new Item((ItemData)data!));

    private static object? Unreachable(IServiceProvider services) => throw new InvalidOperationException("A view model with a loader is created from its data.");
}
