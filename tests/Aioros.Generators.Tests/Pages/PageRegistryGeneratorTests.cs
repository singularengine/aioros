namespace Aioros.Generators.Tests.Pages;

public sealed class PageRegistryGeneratorTests
{
    private const string Models = """
        namespace App.Models;
        public sealed class Store { }
        public sealed class IssueModel(Store store) { public Store Store { get; } = store; }
        public sealed class ShellModel { }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-01")]
    public void PagesAreRegisteredWithTheirRouteSourceAndModel()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Run();

        var page = Assert.Single(result.Pages(out var assembly));
        var store = Activator.CreateInstance(assembly.GetType("App.Models.Store")!)!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal("/issues/{id}", page.Route);
        Assert.Equal("Pages/issues/[id]/Page.axaml", page.Source);
        Assert.Equal("IssueModel", page.CreateModel(new Services(store))!.GetType().Name);
    }

    [Fact]
    [Trait("AC", "GENERATOR-02")]
    public void ModelsTakeTheirServicesFromTheProvider()
    {
        var page = Assert.Single(new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/Page.axaml", "<TextBlock />", "IssueModel")
            .Run()
            .Pages(out var assembly));
        var store = Activator.CreateInstance(assembly.GetType("App.Models.Store")!)!;
        var registered = Activator.CreateInstance(assembly.GetType("App.Models.IssueModel")!, store)!;

        var built = page.CreateModel(new Services(store))!;

        Assert.Same(store, built.GetType().GetProperty("Store")!.GetValue(built));
        Assert.Same(registered, page.CreateModel(new Services(registered, store)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-03")]
    public void LayoutsAreListedOutermostFirst()
    {
        var page = Assert.Single(new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/(app)/Layout.axaml", "<ContentControl />", "ShellModel")
            .Xaml("Pages/Layout.axaml", "<ContentControl />")
            .Xaml("Pages/(app)/inbox/Page.axaml", "<TextBlock />")
            .Run()
            .Pages(out _));

        Assert.Equal(["Pages/Layout.axaml", "Pages/(app)/Layout.axaml"], page.Layouts.Select(layout => layout.Source));
        Assert.Null(page.Layouts[0].CreateModel(new Services()));
        Assert.Equal("ShellModel", page.Layouts[1].CreateModel(new Services())?.GetType().Name);
    }

    [Fact]
    [Trait("AC", "GENERATOR-04")]
    public void LivePagesAreMarked()
    {
        var pages = new GeneratorHarness()
            .Xaml("Pages/live/Page.axaml", "<TextBlock />", extra: " a:Web.Live=\"True\"")
            .Xaml("Pages/still/Page.axaml", "<TextBlock />")
            .Run()
            .Pages(out _);

        Assert.True(pages.Single(page => page.Route == "/live").Live);
        Assert.False(pages.Single(page => page.Route == "/still").Live);
    }

    [Fact]
    [Trait("AC", "GENERATOR-05")]
    public void UnknownModelsAreBuildErrors()
    {
        var diagnostic = Assert.Single(new GeneratorHarness()
            .Xaml("Pages/Page.axaml", "<TextBlock />", "Missing")
            .Run()
            .Diagnostics);

        Assert.Equal("AIOROS001", diagnostic.Id);
        Assert.Equal("/app/Pages/Page.axaml", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    [Trait("AC", "GENERATOR-06")]
    public void ConflictingRoutesAreBuildErrors()
    {
        var diagnostic = Assert.Single(new GeneratorHarness()
            .Xaml("Pages/(a)/about/Page.axaml", "<TextBlock />")
            .Xaml("Pages/(b)/about/Page.axaml", "<TextBlock />")
            .Run()
            .Diagnostics);

        Assert.Equal("AIOROS002", diagnostic.Id);
        Assert.Contains("Pages/(a)/about/Page.axaml", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("Pages/(b)/about/Page.axaml", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "GENERATOR-07")]
    public void TheApplicationTypeIsFound()
    {
        const string Application = """
            namespace Avalonia { public class Application { } }
            namespace App { public sealed class MainApp : Avalonia.Application { } }
            """;

        var found = new GeneratorHarness().Source(Application).Xaml("Pages/Page.axaml", "<TextBlock />").Run().ApplicationType();
        var missing = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().ApplicationType();

        Assert.Equal("MainApp", found?.Name);
        Assert.Null(missing);
    }

    [Fact]
    [Trait("AC", "GENERATOR-08")]
    public void TheHostEntryPointIsGeneratedWhenTheHostIsReferenced()
    {
        const string Host = """
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
                }

                public static class AiorosHost
                {
                    public static AiorosApp CreateBuilder(string[] args, AiorosApp app) => app;
                }
            }
            """;

        var assembly = new GeneratorHarness().Source(Host).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var plain = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();

        var app = assembly.GetType("AiorosApplication")!.GetMethod("CreateBuilder")!.Invoke(null, [Array.Empty<string>()])!;
        Assert.Equal("/", ((System.Collections.Immutable.ImmutableArray<Aioros.Pages.PageDefinition>)app.GetType().GetProperty("Pages")!.GetValue(app)!).Single().Route);
        Assert.Same(assembly, app.GetType().GetProperty("Assembly")!.GetValue(app));
        Assert.Equal("/app", app.GetType().GetProperty("ProjectDirectory")!.GetValue(app));
        Assert.Null(plain.GetType("AiorosApplication"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-55")]
    public void TheBakedServerOriginIsPassedToTheHost()
    {
        const string Host = """
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

                    public string? Server { get; init; }

                    public string? Scheme { get; init; }
                }

                public static class AiorosHost
                {
                    public static AiorosApp CreateBuilder(string[] args, AiorosApp app) => app;
                }
            }
            """;

        var baked = new GeneratorHarness().Source(Host).Property("AiorosServer", "https://shop.example").Property("AiorosScheme", "shop").Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var plain = new GeneratorHarness().Source(Host).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();

        static object? Read(System.Reflection.Assembly assembly, string property)
        {
            var app = assembly.GetType("AiorosApplication")!.GetMethod("CreateBuilder")!.Invoke(null, [Array.Empty<string>()])!;
            return app.GetType().GetProperty(property)!.GetValue(app);
        }

        Assert.Equal("https://shop.example", Read(baked, "Server"));
        Assert.Equal("shop", Read(baked, "Scheme"));
        Assert.Null(Read(plain, "Server"));
        Assert.Null(Read(plain, "Scheme"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-09")]
    public void DuplicatedFilesAreListedOnce()
    {
        var result = new GeneratorHarness()
            .Xaml("Pages/Page.axaml", "<TextBlock />")
            .Xaml("Pages/Page.axaml", "<TextBlock />")
            .Run();

        Assert.Empty(result.Diagnostics);
        Assert.Equal("/", Assert.Single(result.Pages(out _)).Route);
    }

    [Fact]
    [Trait("AC", "GENERATOR-10")]
    public void TheNotFoundPageIsRegistered()
    {
        var registry = new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/Layout.axaml", "<ContentControl />")
            .Xaml("Pages/NotFound.axaml", "<TextBlock />", "ShellModel")
            .Xaml("Pages/Page.axaml", "<TextBlock />")
            .Run()
            .Load()
            .GetType("Aioros.Generated.AiorosPages")!;
        var none = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load().GetType("Aioros.Generated.AiorosPages")!;

        var notFound = (Aioros.Pages.PageDefinition?)registry.GetProperty("NotFound")!.GetValue(null);

        Assert.Equal("Pages/NotFound.axaml", notFound?.Source);
        Assert.Equal(["Pages/Layout.axaml"], notFound?.Layouts.Select(layout => layout.Source));
        Assert.Equal("ShellModel", notFound?.CreateModel(new Services())?.GetType().Name);
        Assert.Null(none.GetProperty("NotFound")!.GetValue(null));
    }

    [Fact]
    [Trait("AC", "GENERATOR-11")]
    public void TheErrorPageIsRegistered()
    {
        var registry = new GeneratorHarness().Xaml("Pages/Error.axaml", "<TextBlock />").Run().Load().GetType("Aioros.Generated.AiorosPages")!;
        var none = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load().GetType("Aioros.Generated.AiorosPages")!;

        Assert.Equal("Pages/Error.axaml", ((Aioros.Pages.PageDefinition?)registry.GetProperty("Error")!.GetValue(null))?.Source);
        Assert.Null(none.GetProperty("Error")!.GetValue(null));
    }

    [Fact]
    [Trait("AC", "GENERATOR-12")]
    public void TypedRoutesBuildUrls()
    {
        var routes = new GeneratorHarness()
            .Xaml("Pages/Page.axaml", "<TextBlock />")
            .Xaml("Pages/issues/Page.axaml", "<TextBlock />")
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />")
            .Xaml("Pages/(edit)/issues/[id:int]/edit/Page.axaml", "<TextBlock />")
            .Xaml("Pages/docs/[...slug]/Page.axaml", "<TextBlock />")
            .Run()
            .Load()
            .GetType("Aioros.Generated.Routes")!;

        string Call(string name, params object[] values) => (string)routes.GetMethod(name)!.Invoke(null, values)!;

        Assert.Equal("/", Call("Home"));
        Assert.Equal("/issues", Call("Issues"));
        Assert.Equal("/issues/LUM%201%2F2", Call("IssuesById", "LUM 1/2"));
        Assert.Equal("/issues/7/edit", Call("IssuesEditById", 7));
        Assert.Equal("/docs/guide/a%20b", Call("DocsBySlug", "guide/a b"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-13")]
    public void SourcesAreEmbeddedOnRequest()
    {
        var embedded = new GeneratorHarness()
            .Property("AiorosEmbedSources", "true")
            .Xaml("Pages/Page.axaml", "<TextBlock Text=\"Say \"\"hi\"\"\" />")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />")
            .Run()
            .Load()
            .GetType("Aioros.Generated.AiorosSources")!;
        var plain = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load().GetType("Aioros.Generated.AiorosSources");

        string? Text(string path) => (string?)embedded.GetMethod("Text")!.Invoke(null, [path]);

        Assert.Contains("<TextBlock Text=\"Say \"\"hi\"\"\" />", Text("Pages/Page.axaml"), StringComparison.Ordinal);
        Assert.Contains("\n", Text("Pages/Page.axaml"), StringComparison.Ordinal);
        Assert.Contains("<a:Outlet />", Text("Pages/Layout.axaml"), StringComparison.Ordinal);
        Assert.Null(Text("Pages/missing.axaml"));
        Assert.Null(plain);
    }
}
