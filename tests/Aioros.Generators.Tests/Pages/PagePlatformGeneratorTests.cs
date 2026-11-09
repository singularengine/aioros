using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class PagePlatformGeneratorTests
{
    [Fact]
    [Trait("AC", "NATIVE-06")]
    public void AvaloniasOnPlatformIsReportedWhereTheWebRendersIt()
    {
        var result = new GeneratorHarness()
            .Xaml("Pages/markup/Page.axaml", "<TextBlock Text=\"{OnPlatform Default=a, Linux=b}\" />")
            .Xaml("Pages/element/Page.axaml", "<TextBlock><TextBlock.Text><OnPlatform Default=\"a\" Linux=\"b\" /></TextBlock.Text></TextBlock>")
            .Xaml("Pages/aioros/Page.axaml", "<TextBlock Text=\"{a:OnPlatform Default=a, Linux=b, Web=c}\" />")
            .Xaml("Pages/native/Page.axaml", "<TextBlock Text=\"{OnPlatform Default=a, Linux=b}\" />", extra: " a:Page.Platforms=\"Desktop\"")
            .Run();

        var warnings = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS030").ToList();
        Assert.Equal(["/app/Pages/element/Page.axaml", "/app/Pages/markup/Page.axaml"], warnings.Select(warning => warning.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
        Assert.All(warnings, warning => Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, warning.Severity));
        Assert.All(warnings, warning => Assert.Contains("a:OnPlatform", warning.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "NATIVE-07")]
    public void PlatformsAreReadFromThePageRoot()
    {
        var result = new GeneratorHarness()
            .Xaml("Pages/files/Page.axaml", "<TextBlock />", extra: " a:Page.Platforms=\"Desktop\"")
            .Xaml("Pages/camera/Page.axaml", "<TextBlock />", extra: " a:Page.Platforms=\"Desktop, Mobile\"")
            .Xaml("Pages/plain/Page.axaml", "<TextBlock />")
            .Run();
        var pages = result.Pages(out _);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(PagePlatforms.Desktop, pages.Single(page => page.Route == "/files").Platforms);
        Assert.Equal(PagePlatforms.Desktop | PagePlatforms.Mobile, pages.Single(page => page.Route == "/camera").Platforms);
        Assert.Equal(PagePlatforms.All, pages.Single(page => page.Route == "/plain").Platforms);
        Assert.True(pages.Single(page => page.Route == "/files").NativeOnly);
        Assert.False(pages.Single(page => page.Route == "/plain").NativeOnly);

        var unknown = new GeneratorHarness().Xaml("Pages/files/Page.axaml", "<TextBlock />", extra: " a:Page.Platforms=\"Desktop, Watch\"").Run();
        var error = Assert.Single(unknown.Diagnostics, diagnostic => diagnostic.Id == "AIOROS031");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Watch'", error.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Web, Desktop, Mobile", error.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NATIVE-08")]
    public void TheAppsStandInIsTheNativeOnlyPage()
    {
        var result = new GeneratorHarness()
            .Xaml("Pages/Layout.axaml", "<a:Outlet />")
            .Xaml("Pages/NativeOnly.axaml", "<TextBlock Text=\"Get the app\" />")
            .Xaml("Pages/files/Page.axaml", "<TextBlock />", extra: " a:Page.Platforms=\"Desktop\"")
            .Run();
        var pages = result.Pages(out var assembly);
        var standIn = (PageDefinition?)assembly.GetType("Aioros.Generated.AiorosPages")!.GetProperty("NativeOnly")!.GetValue(null);

        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(pages, page => page.Source == "Pages/NativeOnly.axaml");
        Assert.NotNull(standIn);
        Assert.Equal("/_native-only", standIn.Route);
        Assert.Equal(["Pages/Layout.axaml"], standIn.Layouts.Select(layout => layout.Source));

        var without = new GeneratorHarness().Xaml("Pages/files/Page.axaml", "<TextBlock />").Run();
        without.Pages(out var plain);
        Assert.Null(plain.GetType("Aioros.Generated.AiorosPages")!.GetProperty("NativeOnly")!.GetValue(null));

        var hosted = new GeneratorHarness().Source(Host).Xaml("Pages/NativeOnly.axaml", "<TextBlock />").Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var app = hosted.GetType("AiorosApplication")!.GetMethod("CreateBuilder")!.Invoke(null, [Array.Empty<string>()])!;
        Assert.Equal("/_native-only", ((PageDefinition?)app.GetType().GetProperty("NativeOnly")!.GetValue(app))?.Route);
    }

    private const string Host = """
        namespace Aioros.Hosting
        {
            public sealed record AiorosApp(
                System.Collections.Immutable.ImmutableArray<Aioros.Pages.PageDefinition> Pages,
                System.Func<System.Type>? ApplicationType,
                System.Reflection.Assembly Assembly,
                string ProjectDirectory,
                Aioros.Pages.PageDefinition? NotFound = null,
                Aioros.Pages.PageDefinition? Error = null)
            {
                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Functions.ServerFunctionGroup> Functions { get; init; }

                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Pages.NavigationInterface> Navigators { get; init; }

                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Rendering.MemberAccessor> Accessors { get; init; }

                public Aioros.Pages.PageDefinition? NativeOnly { get; init; }
            }

            public static class AiorosHost
            {
                public static AiorosApp CreateBuilder(string[] args, AiorosApp app) => app;
            }
        }
        """;
}
