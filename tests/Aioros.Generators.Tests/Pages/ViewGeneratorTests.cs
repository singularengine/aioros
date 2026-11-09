using System.Collections.Immutable;
using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class ViewGeneratorTests
{
    private const string Models = """
        namespace Microsoft.AspNetCore.Authorization
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class AuthorizeAttribute : System.Attribute
            {
                public string? Policy { get; set; }
                public string? Roles { get; set; }
            }
        }

        namespace App.Models
        {
            public sealed class Store { }
            public sealed record RecentData(string Name);
            public sealed record OtherData(string Name, int Count);
            public sealed class RecentModel(RecentData data, Store store) { public RecentData Data { get; } = data; public Store Store { get; } = store; }
            public sealed class StoreModel(Store store) { public Store Store { get; } = store; }
            public sealed class PlainModel { }
        }

        namespace App.Views
        {
            public partial class Recent { }
            public partial class Badge { }
            public partial class Outer { }
            public partial class Plain { }
            public partial class Ping { }
            public partial class Pong { }
        }
        """;

    private const string Views = " xmlns:v=\"using:App.Views\"";

    private static string Loader(string data, string body, string attributes = "") => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        {{attributes}}
        public sealed class RecentLoader : ILoader<App.Models.{{data}}>
        {
            public ValueTask<LoaderResult<App.Models.{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.{{data}}>>(new App.Models.{{data}}({{body}}));
        }
        """;

    private static GeneratorHarness Recent(string data = "RecentData", string body = "\"recent\"", string attributes = "") => new GeneratorHarness()
        .Source(Models)
        .Source(Loader(data, body, attributes), "Views/Recent.Loader.cs")
        .Xaml("Views/Recent.axaml", "<TextBlock />", "RecentModel", " x:Class=\"App.Views.Recent\"");

    [Fact]
    [Trait("AC", "GENERATOR-40")]
    public async Task AViewByItsClassBringsItsLoaderAndViewModelAsync()
    {
        var result = Recent()
            .Xaml("Pages/home/Page.axaml", "<StackPanel><TextBlock /><v:Recent /></StackPanel>", "PlainModel", Views)
            .Xaml("Pages/other/Page.axaml", "<w:Recent />", extra: " xmlns:w=\"clr-namespace:App.Views;assembly=App\"")
            .Run();
        var pages = result.Pages(out var assembly).ToDictionary(page => page.Route);
        var view = Assert.Single(pages["/home"].Views);
        var store = Activator.CreateInstance(assembly.GetType("App.Models.Store")!)!;

        var data = (await view.View.Loader!.LoadAsync(new Services(store), PageRequest.For("/home"), TestContext.Current.CancellationToken)).Data;
        var model = view.View.Loader.CreateModel(new Services(store), data)!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal(("Pages/home/Page.axaml:0.1", 0, -1, "Views/Recent.axaml", "App.Views.Recent"), (view.Key, view.Segment, view.Parent, view.View.Source, view.View.Type?.Invoke().FullName));
        Assert.Equal("recent", data!.GetType().GetProperty("Name")!.GetValue(data));
        Assert.Same(data, model.GetType().GetProperty("Data")!.GetValue(model));
        Assert.Same(store, model.GetType().GetProperty("Store")!.GetValue(model));
        Assert.Equal("Pages/other/Page.axaml:0", Assert.Single(pages["/other"].Views).Key);
        Assert.Same(view.View, pages["/other"].Views[0].View);
    }

    [Fact]
    [Trait("AC", "GENERATOR-41")]
    public void ViewsAreListedInDocumentOrderWithTheirSegmentsAndNesting()
    {
        var result = Recent()
            .Xaml("Views/Badge.axaml", "<TextBlock />", "PlainModel", " x:Class=\"App.Views.Badge\"")
            .Xaml("Views/Outer.axaml", "<v:Recent />", "PlainModel", " x:Class=\"App.Views.Outer\"" + Views)
            .Xaml("Pages/Layout.axaml", "<StackPanel><v:Badge /><a:Outlet /></StackPanel>", extra: Views)
            .Xaml("Pages/inbox/Page.axaml", "<TextBlock />", "PlainModel")
            .Xaml("Pages/home/Page.axaml", """
                <StackPanel>
                  <a:View Source="/Pages/inbox/Page.axaml" />
                  <v:Outer />
                  <ItemsControl><ItemsControl.ItemTemplate><DataTemplate><v:Badge /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </StackPanel>
                """, extra: Views)
            .Run();
        var pages = result.Pages(out _).ToDictionary(page => page.Route);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            [
                ("Pages/Layout.axaml:0.0", 0, -1, "Views/Badge.axaml"),
                ("Pages/home/Page.axaml:0.0", 1, -1, "Pages/inbox/Page.axaml"),
                ("Pages/home/Page.axaml:0.1", 1, -1, "Views/Outer.axaml"),
                ("Pages/home/Page.axaml:0.1/0", 1, 2, "Views/Recent.axaml"),
            ],
            pages["/home"].Views.Select(view => (view.Key, view.Segment, view.Parent, view.View.Source)));
        Assert.Null(pages["/home"].Views[1].View.Type);
        Assert.Equal(["Views/Badge.axaml"], pages["/inbox"].Views.Select(view => view.View.Source));
        Assert.Equal("Pages/inbox/Page.axaml", pages["/inbox"].Source);
    }

    [Fact]
    [Trait("AC", "GENERATOR-42")]
    public void AViewWithoutALoaderGetsItsViewModelFromDependencyInjection()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Xaml("Views/Plain.axaml", "<TextBlock />", "StoreModel", " x:Class=\"App.Views.Plain\"")
            .Xaml("Pages/Page.axaml", "<v:Plain />", extra: Views)
            .Run();
        var view = Assert.Single(Assert.Single(result.Pages(out var assembly)).Views).View;
        var store = Activator.CreateInstance(assembly.GetType("App.Models.Store")!)!;
        var registered = Activator.CreateInstance(assembly.GetType("App.Models.StoreModel")!, store)!;

        var built = view.CreateModel(new Services(store))!;

        Assert.Empty(result.Diagnostics);
        Assert.Null(view.Loader);
        Assert.Same(store, built.GetType().GetProperty("Store")!.GetValue(built));
        Assert.Same(registered, view.CreateModel(new Services(registered, store)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-43")]
    public void UnresolvableViewsAreBuildErrors()
    {
        var result = new GeneratorHarness()
            .Source(Models)
            .Xaml("Views/Ping.axaml", "<v:Pong />", "PlainModel", " x:Class=\"App.Views.Ping\"" + Views)
            .Xaml("Views/Pong.axaml", "<v:Ping />", "PlainModel", " x:Class=\"App.Views.Pong\"" + Views)
            .Xaml("Pages/loop/Page.axaml", "<v:Ping />", extra: Views)
            .Xaml("Pages/missing/Page.axaml", "<a:View Source=\"/Pages/gone/Page.axaml\" />")
            .Run();
        var pages = result.Pages(out _).ToDictionary(page => page.Route);
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS018").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, message => message.StartsWith("Views/Pong.axaml:", StringComparison.Ordinal) && message.Contains("places itself through Views/Ping.axaml > Views/Pong.axaml > Views/Ping.axaml", StringComparison.Ordinal));
        Assert.Contains(errors, message => message.StartsWith("Pages/missing/Page.axaml:", StringComparison.Ordinal) && message.Contains("'/Pages/gone/Page.axaml'", StringComparison.Ordinal));
        Assert.Equal(["Views/Ping.axaml", "Views/Pong.axaml"], pages["/loop"].Views.Select(view => view.View.Source));
        Assert.Empty(pages["/missing"].Views);
    }

    [Fact]
    [Trait("AC", "GENERATOR-44")]
    public void ViewsCarryTheirAccessAndTheirDataShapesInThePagesContract()
    {
        static ImmutableDictionary<string, PageDefinition> Build(GeneratorHarness harness) =>
            harness
                .Xaml("Pages/home/Page.axaml", "<v:Recent />", extra: Views)
                .Xaml("Pages/plain/Page.axaml", "<TextBlock />")
                .Run()
                .Pages(out _)
                .ToImmutableDictionary(page => page.Route);
        var guarded = Build(Recent(attributes: "[Microsoft.AspNetCore.Authorization.Authorize(Roles = \"admin\")]"));
        var reshaped = Build(Recent("OtherData", "\"recent\", 3").Source("namespace App.Models; public sealed class OtherModel(OtherData data) { }"));
        var withoutViews = new GeneratorHarness().Source(Models).Xaml("Pages/plain/Page.axaml", "<TextBlock />").Run().Pages(out _)[0];

        Assert.Equal([new PageAccess(null, "admin")], guarded["/home"].Views[0].View.Access);
        Assert.Empty(guarded["/home"].Access);
        Assert.NotEqual(guarded["/home"].DataContract, reshaped["/home"].DataContract);
        Assert.NotEqual(guarded["/home"].DataContract, guarded["/plain"].DataContract);
        Assert.Equal(withoutViews.DataContract, guarded["/plain"].DataContract);
    }
}
