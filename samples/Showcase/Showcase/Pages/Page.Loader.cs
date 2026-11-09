using Aioros.Generated;
using Aioros.Pages;
using Showcase.Models;

namespace Showcase.Pages;

public static class HomeLoader
{
    public static LoaderResult<HomeData> Load() =>
        LoaderResult.With(
            new HomeData(AiorosSources.Text("Pages/Page.axaml") ?? string.Empty),
            "Aioros Showcase",
            new PageMetadata("A catalog of live apps written once in XAML: finance, CRM and music."));
}
