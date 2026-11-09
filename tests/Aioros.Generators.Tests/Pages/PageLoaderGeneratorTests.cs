using System.Collections.Immutable;
using System.Globalization;
using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class PageLoaderGeneratorTests
{
    private const string Models = """
        namespace App.Models;
        public sealed class Store { }
        public sealed record IssueData(string Id);
        public sealed record ShellData(string User);
        public sealed class IssueModel(IssueData data, Store store) { public IssueData Data { get; } = data; public Store Store { get; } = store; }
        public sealed class ShellModel(ShellData data) { public ShellData Data { get; } = data; }
        public sealed class PlainModel { }
        """;

    private static string Loader(string name, string data, string body, string parameters = "") => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        public sealed class {{name}}({{parameters}}) : ILoader<App.Models.{{data}}>
        {
            public ValueTask<LoaderResult<App.Models.{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.{{data}}>>(new App.Models.{{data}}({{body}}));
        }
        """;

    private static PageRequest Request(string id) =>
        new("/issues/" + id, ImmutableDictionary<string, string>.Empty.Add("id", id), ImmutableDictionary<string, string>.Empty);

    [Fact]
    [Trait("AC", "GENERATOR-14")]
    public async Task PageLoadersFeedTheViewModelsConstructorAsync()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("IssueLoader", "IssueData", "store is null ? \"\" : request.Value(\"id\")!", "App.Models.Store store"), "Pages/issues/[id]/Page.Loader.cs")
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Run();
        var page = Assert.Single(result.Pages(out var assembly));
        var store = Activator.CreateInstance(assembly.GetType("App.Models.Store")!)!;
        var registered = Activator.CreateInstance(assembly.GetType("App.Loaders.IssueLoader")!, [null])!;

        var data = (await page.Loader!.LoadAsync(new Services(store), Request("7"), TestContext.Current.CancellationToken)).Data;
        var fromRegistered = (await page.Loader.LoadAsync(new Services(registered), Request("8"), TestContext.Current.CancellationToken)).Data;
        var model = page.Loader.CreateModel(new Services(store), data)!;
        var registeredModel = Activator.CreateInstance(assembly.GetType("App.Models.IssueModel")!, data, store)!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal("7", data!.GetType().GetProperty("Id")!.GetValue(data));
        Assert.Equal(string.Empty, fromRegistered!.GetType().GetProperty("Id")!.GetValue(fromRegistered));
        Assert.Same(data, model.GetType().GetProperty("Data")!.GetValue(model));
        Assert.Same(store, model.GetType().GetProperty("Store")!.GetValue(model));
        Assert.NotSame(registeredModel, page.Loader.CreateModel(new Services(registeredModel, store), data));
    }

    [Fact]
    [Trait("AC", "GENERATOR-15")]
    public async Task PageAndLayoutLoadersAreToldApartByFileNameAsync()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("ShellLoader", "ShellData", "\"ada\""), "Pages/Layout.Loader.cs")
            .Source(Loader("HomeLoader", "IssueData", "\"home\""), "Pages/Page.Loader.cs")
            .Source("namespace App.Models; public sealed class HomeModel(IssueData data, Store store) { }")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
            .Xaml("Pages/Page.axaml", "<TextBlock />", "HomeModel")
            .Xaml("Pages/(app)/Layout.axaml", "<a:Outlet />")
            .Xaml("Pages/(app)/inbox/Page.axaml", "<TextBlock />")
            .Run();
        var pages = result.Pages(out _);
        var home = pages.Single(page => page.Route == "/");
        var inbox = pages.Single(page => page.Route == "/inbox");

        var shell = (await home.Layouts[0].Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken)).Data;
        var own = (await home.Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken)).Data;
        var inherited = (await inbox.Layouts[0].Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken)).Data;

        Assert.Empty(result.Diagnostics);
        Assert.Equal("ShellData", shell!.GetType().Name);
        Assert.Equal("IssueData", own!.GetType().Name);
        Assert.Equal("ShellModel", home.Layouts[0].Loader!.CreateModel(new Services(), shell)!.GetType().Name);
        Assert.Null(inbox.Loader);
        Assert.Equal(shell, inherited);
        Assert.Null(inbox.Layouts[1].Loader);
    }

    [Fact]
    [Trait("AC", "GENERATOR-16")]
    public void PagesWithoutLoadersAreBuiltAsBefore()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "PlainModel")
            .Xaml("Pages/Page.axaml", "<TextBlock />", "PlainModel")
            .Run();
        var page = Assert.Single(result.Pages(out var assembly));
        var registered = Activator.CreateInstance(assembly.GetType("App.Models.PlainModel")!)!;

        Assert.Empty(result.Diagnostics);
        Assert.Null(page.Loader);
        Assert.Null(page.Layouts[0].Loader);
        Assert.Equal("PlainModel", page.CreateModel(new Services())!.GetType().Name);
        Assert.Same(registered, page.Layouts[0].CreateModel(new Services(registered)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-17")]
    public void ViewModelsAskingForDataNoLoaderProvidesAreBuildErrors()
    {
        var diagnostics = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("IssueLoader", "IssueData", "\"7\""), "Pages/issues/[id]/Page.Loader.cs")
            .Source(Loader("WrongLoader", "ShellData", "\"ada\""), "Pages/other/Page.Loader.cs")
            .Source("namespace App.Models; public sealed class OtherModel(IssueData issue, ShellData shell) { }")
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/issues/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/other/Page.axaml", "<TextBlock />", "OtherModel")
            .Run()
            .Diagnostics;

        Assert.Equal(["AIOROS004", "AIOROS004"], diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.Equal(["/app/Pages/issues/Page.axaml", "/app/Pages/other/Page.axaml"], diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
        Assert.All(diagnostics, diagnostic => Assert.Contains("App.Models.IssueData", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-18")]
    public void LoaderDataTheViewModelDoesNotTakeIsABuildError()
    {
        var diagnostics = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("PlainLoader", "IssueData", "\"7\""), "Pages/plain/Page.Loader.cs")
            .Source(Loader("BareLoader", "ShellData", "\"ada\""), "Pages/Layout.Loader.cs")
            .Xaml("Pages/plain/Page.axaml", "<TextBlock />", "PlainModel")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />")
            .Run()
            .Diagnostics;

        Assert.Equal(["AIOROS005", "AIOROS005"], diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.Equal(["/app/Pages/Layout.axaml", "/app/Pages/plain/Page.axaml"], diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("App.Loaders.PlainLoader", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-38")]
    public async Task LoadersWithoutDataNeedNoViewModelAsync()
    {
        static string Dataless(string name, string result) => $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Pages;
            namespace App.Loaders;
            public sealed class {{name}} : ILoader
            {
                public ValueTask<LoaderOutcome> LoadAsync(PageRequest request, CancellationToken cancellationToken) => ValueTask.FromResult({{result}});
            }
            """;
        GeneratorHarness App(bool loaded)
        {
            var harness = new GeneratorHarness()
                .Source(Models)
                .Xaml("Pages/Page.axaml", "<TextBlock />")
                .Xaml("Pages/about/Page.axaml", "<TextBlock />", "PlainModel");
            return loaded
                ? harness
                    .Source(Dataless("HomeLoader", "LoaderResult.Redirect(\"/issues\")"), "Pages/Page.Loader.cs")
                    .Source(Dataless("AboutLoader", "new LoaderOutcome { Title = \"About\" }"), "Pages/about/Page.Loader.cs")
                : harness;
        }

        var result = App(loaded: true).Run();
        var pages = result.Pages(out var assembly);
        var home = pages.Single(page => page.Route == "/");
        var about = pages.Single(page => page.Route == "/about");
        var registered = Activator.CreateInstance(assembly.GetType("App.Models.PlainModel")!)!;

        var redirect = await home.Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken);
        var titled = await about.Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(LoaderResult.Redirect("/issues"), redirect);
        Assert.Equal(new LoaderOutcome { Title = "About" }, titled);
        Assert.Null(home.Loader.CreateModel(new Services(), null));
        Assert.Equal("App.Models.PlainModel", about.Loader.CreateModel(new Services(), null)?.GetType().FullName);
        Assert.Same(registered, about.Loader.CreateModel(new Services(registered), null));
        Assert.Equal("null", Written(about.Loader));
        using var empty = System.Text.Json.JsonDocument.Parse("null");
        Assert.Null(home.Loader.Read!(empty.RootElement));
        Assert.Equal(App(loaded: false).Run().Pages(out _).Select(page => page.DataContract), pages.Select(page => page.DataContract));
    }

    [Fact]
    [Trait("AC", "GENERATOR-19")]
    public void LoadersAwayFromTheirPageAreBuildErrors()
    {
        var diagnostics = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("LooseLoader", "IssueData", "\"7\""), "Data/Loaders.cs")
            .Source(Loader("OrphanLoader", "IssueData", "\"7\""), "Pages/missing/Page.Loader.cs")
            .Source("namespace App.Loaders; public static class TypedLoader { public static App.Models.IssueData Load(string? id) => new(id ?? \"\"); }", "Pages/gone/Page.Loader.cs")
            .Source("namespace App.Loaders; public sealed record Helper(int Id); public static class Helpers { public static int Twice(int value) => value * 2; }", "Pages/Page.Loader.cs")
            .Source(Loader("FirstLoader", "ShellData", "\"a\"") + "\n" + Loader("SecondLoader", "ShellData", "\"b\"").Replace("using System.Threading;\nusing System.Threading.Tasks;\nusing Aioros.Pages;\nnamespace App.Loaders;\n", string.Empty, StringComparison.Ordinal), "Pages/Layout.Loader.cs")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
            .Xaml("Pages/Page.axaml", "<TextBlock />")
            .Run()
            .Diagnostics;

        Assert.Equal(["AIOROS006", "AIOROS006", "AIOROS006", "AIOROS006"], diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.Equal(
            ["/app/Data/Loaders.cs", "/app/Pages/Layout.Loader.cs", "/app/Pages/gone/Page.Loader.cs", "/app/Pages/missing/Page.Loader.cs"],
            diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-20")]
    public async Task LoaderResultsReachTheRuntimeUnchangedAsync()
    {
        const string loader = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Pages;
            namespace App.Loaders;
            public sealed class IssueLoader : ILoader<App.Models.IssueData>
            {
                public async ValueTask<LoaderResult<App.Models.IssueData>> LoadAsync(PageRequest request, CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    return request.Value("id") switch
                    {
                        "missing" => LoaderResult.NotFound(),
                        "old" => LoaderResult.Redirect("/issues/new"),
                        var id => new LoaderResult<App.Models.IssueData>(new App.Models.IssueData(id)) { Title = "Issue " + id, Metadata = new PageMetadata("About " + id) },
                    };
                }
            }
            """;
        var page = Assert.Single(new GeneratorHarness()
            .Source(Models)
            .Source(loader, "Pages/issues/[id]/Page.Loader.cs")
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Run()
            .Pages(out _));

        var missing = await page.Loader!.LoadAsync(new Services(), Request("missing"), TestContext.Current.CancellationToken);
        var moved = await page.Loader.LoadAsync(new Services(), Request("old"), TestContext.Current.CancellationToken);
        var found = await page.Loader.LoadAsync(new Services(), Request("7"), TestContext.Current.CancellationToken);

        Assert.Equal((true, null, null), (missing.NotFound, missing.RedirectTo, missing.Data));
        Assert.Equal((false, "/issues/new"), (moved.NotFound, moved.RedirectTo));
        Assert.Equal(("Issue 7", "About 7", "7"), (found.Title, found.Metadata?.Description, found.Data?.GetType().GetProperty("Id")?.GetValue(found.Data)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-21")]
    public void DeferredLoaderDataIsABuildError()
    {
        var diagnostics = Shapes(
            ("query", "System.Linq.IQueryable<App.Models.Issue>"),
            ("untyped", "System.Linq.IQueryable"),
            ("stream", "System.Collections.Generic.IAsyncEnumerable<App.Models.Issue>"),
            ("set", "App.Models.IIssues"),
            ("board", "App.Models.Board"),
            ("feed", "App.Models.Feed"));

        Assert.Equal(Enumerable.Repeat("AIOROS007", 6), diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.Equal(
            ["board", "feed", "query", "set", "stream", "untyped"],
            diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path.Split('/')[^2]).Order(StringComparer.Ordinal));
        var messages = diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(messages, message => message.Contains("App.Loaders.boardLoader", StringComparison.Ordinal) && message.Contains("'Issues'", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'Items'", StringComparison.Ordinal));
        Assert.All(messages, message => Assert.Contains("must be materialized (a list or array)", message, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-22")]
    public void MaterializedLoaderDataBuildsCleanly()
    {
        var diagnostics = Shapes(
            ("list", "System.Collections.Generic.List<App.Models.Issue>"),
            ("array", "App.Models.Issue[]"),
            ("summary", "App.Models.Summary"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    [Trait("AC", "GENERATOR-29")]
    public void LoaderDataTheInstalledAppCannotReadIsABuildError()
    {
        var diagnostics = Shapes(
            ("untyped", "App.Models.Untyped"),
            ("contract", "App.Models.Contract"),
            ("action", "App.Models.Action"),
            ("stream", "App.Models.Body"),
            ("keys", "App.Models.Keys"),
            ("built", "App.Models.Built"),
            ("deep", "App.Models.Deep"),
            ("query", "System.Linq.IQueryable<App.Models.Issue>"),
            ("summary", "App.Models.Summary"));
        var messages = diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS011")
            .ToDictionary(diagnostic => diagnostic.Location.GetLineSpan().Path.Split('/')[^2], diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture));

        Assert.Equal(["AIOROS007"], diagnostics.Where(diagnostic => diagnostic.Id != "AIOROS011").Select(diagnostic => diagnostic.Id));
        var hidden = new GeneratorHarness().Source("""
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Pages;
            namespace App.Models;
            public static class Holder
            {
                private sealed record Secret(string Value);
                private sealed class SecretLoader : ILoader<Secret>
                {
                    public ValueTask<LoaderResult<Secret>> LoadAsync(PageRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(default(LoaderResult<Secret>));
                }
            }
            """, "Pages/hidden/Page.Loader.cs").Xaml("Pages/hidden/Page.axaml", "<TextBlock />").Run().Diagnostics;
        messages["hidden"] = Assert.Single(hidden, diagnostic => diagnostic.Id == "AIOROS011").GetMessage(CultureInfo.InvariantCulture);

        Assert.Equal(["action", "built", "contract", "deep", "hidden", "keys", "stream", "untyped"], messages.Keys.Order(StringComparer.Ordinal));
        Assert.All(messages.Values, message => Assert.Contains("cannot be sent to the installed app", message, StringComparison.Ordinal));
        Assert.Contains("The loader 'App.Loaders.untypedLoader' returns 'App.Models.Untyped'", messages["untyped"], StringComparison.Ordinal);
        Assert.Contains("'Untyped.Value' is 'object'", messages["untyped"], StringComparison.Ordinal);
        Assert.Contains("'Contract.Owner' is 'System.IDisposable'", messages["contract"], StringComparison.Ordinal);
        Assert.Contains("'Action.Run' is a delegate", messages["action"], StringComparison.Ordinal);
        Assert.Contains("'Body.Content' is 'System.IO.Stream'", messages["stream"], StringComparison.Ordinal);
        Assert.Contains("'Keys.Map' is a dictionary whose keys are not strings", messages["keys"], StringComparison.Ordinal);
        Assert.Contains("constructor parameter 'size' matches no public property", messages["built"], StringComparison.Ordinal);
        Assert.Contains("'Deep.Shapes[].Shape' is 'App.Models.Shape'", messages["deep"], StringComparison.Ordinal);
        Assert.Contains("[JsonPolymorphic]", messages["deep"], StringComparison.Ordinal);
        Assert.Contains("[JsonDerivedType(typeof(", messages["deep"], StringComparison.Ordinal);
        Assert.Contains("cannot reach", messages["hidden"], StringComparison.Ordinal);
    }

    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Shapes(params (string Name, string Data)[] shapes)
    {
        var harness = new GeneratorHarness().Source("""
            namespace App.Models;
            public sealed record Issue(string Id);
            public interface IIssues : System.Linq.IQueryable<Issue> { }
            public sealed record Board(string Name, System.Linq.IQueryable<Issue> Issues);
            public sealed class Feed { public System.Linq.IQueryable? Items { get; init; } }
            public sealed record Summary(string Name, int Count, System.Collections.Generic.List<Issue> Issues, Issue[] Top);
            public sealed record Untyped(object Value);
            public sealed record Contract(System.IDisposable Owner);
            public sealed record Action(System.Action Run);
            public sealed record Body(System.IO.Stream Content);
            public sealed record Keys(System.Collections.Generic.Dictionary<int, string> Map);
            public sealed class Built { public Built(int size) { } public int Count { get; } }
            public abstract class Shape { public string Name { get; init; } = ""; }
            public sealed record Holding(Shape Shape);
            public sealed record Deep(System.Collections.Generic.IReadOnlyList<Holding> Shapes);
            """);
        foreach (var (name, data) in shapes)
        {
            harness = harness
                .Source($$"""
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Aioros.Pages;
                    namespace App.Loaders
                    {
                        public sealed class {{name}}Loader : ILoader<{{data}}>
                        {
                            public ValueTask<LoaderResult<{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                                ValueTask.FromResult(default(LoaderResult<{{data}}>));
                        }
                    }
                    namespace App.Models
                    {
                        public sealed class {{name}}Model({{data}} data) { public {{data}} Data { get; } = data; }
                    }
                    """, $"Pages/{name}/Page.Loader.cs")
                .Xaml($"Pages/{name}/Page.axaml", "<TextBlock />", $"{name}Model");
        }

        return harness.Run().Diagnostics;
    }

    private static string Written(LoaderDefinition loader)
    {
        using var buffer = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            loader.Write!(writer, null);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
