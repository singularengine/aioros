using System.Globalization;
using Aioros.Pages;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Tests.Pages;

public sealed class RepeatedViewGeneratorTests
{
    private const string Models = """
        namespace App.Models
        {
            public sealed record Card(string Id, string Title);
            public sealed record CardData(string Id, int Comments);
            public sealed record Column(string Name, Card[] Cards);
            public sealed class BoardModel
            {
                public Card[] Cards { get; } = [new("a", "First"), new("b", "Second")];
                public Column[] Columns { get; } = [];
            }

            public sealed class CardModel(CardData data) { public CardData Data { get; } = data; }
            public sealed class PlainModel { }
        }

        namespace App.Views
        {
            public partial class CardView { }
            public partial class ItemView { }
            public partial class PlainItem { }
            public partial class SingleView { }
            public partial class Shell { }
        }

        """;

    private const string Views = " xmlns:v=\"using:App.Views\"";

    private const string Usings = "using System.Collections.Generic;\nusing System.Linq;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Aioros.Pages;\nnamespace App.Loaders;\n";

    private const string CardLoader = Usings + """
        public sealed class CardViewLoader : IBatchLoader<string, App.Models.CardData>
        {
            public ValueTask<IReadOnlyDictionary<string, App.Models.CardData>> LoadAsync(IReadOnlyList<string> keys, PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<IReadOnlyDictionary<string, App.Models.CardData>>(keys.Where(key => key != "gone").ToDictionary(key => key, key => new App.Models.CardData(key, key.Length)));
        }
        """;

    private const string ItemLoader = Usings + """
        public sealed class ItemViewLoader : IBatchLoader<App.Models.Card, App.Models.CardData>
        {
            public ValueTask<IReadOnlyDictionary<App.Models.Card, App.Models.CardData>> LoadAsync(IReadOnlyList<App.Models.Card> keys, PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<IReadOnlyDictionary<App.Models.Card, App.Models.CardData>>(keys.ToDictionary(key => key, key => new App.Models.CardData(key.Id, key.Title.Length)));
        }
        """;

    private const string SingleLoader = Usings + """
        public sealed class SingleViewLoader : ILoader<App.Models.CardData>
        {
            public ValueTask<LoaderResult<App.Models.CardData>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.CardData>>(new App.Models.CardData("one", 1));
        }
        """;

    private static GeneratorHarness App() => new GeneratorHarness()
        .Source(Models)
        .Source(CardLoader, "Views/CardView.Loader.cs")
        .Source(ItemLoader, "Views/ItemView.Loader.cs")
        .Source(SingleLoader, "Views/SingleView.Loader.cs")
        .Xaml("Views/CardView.axaml", "<TextBlock />", "CardModel", " x:Class=\"App.Views.CardView\"")
        .Xaml("Views/ItemView.axaml", "<TextBlock />", "CardModel", " x:Class=\"App.Views.ItemView\"")
        .Xaml("Views/PlainItem.axaml", "<TextBlock />", "Card", " x:Class=\"App.Views.PlainItem\"")
        .Xaml("Views/SingleView.axaml", "<TextBlock />", "CardModel", " x:Class=\"App.Views.SingleView\"");

    private static string List(string view, string items = "Cards") =>
        $"<ItemsControl ItemsSource=\"{{Binding {items}}}\"><ItemsControl.ItemTemplate><DataTemplate x:DataType=\"vm:Card\">{view}</DataTemplate></ItemsControl.ItemTemplate></ItemsControl>";

    [Fact]
    [Trait("AC", "GENERATOR-46")]
    public async Task AViewInAListsItemTemplateRepeatsWithItsKeyAndBatchLoaderAsync()
    {
        var result = Placed(App(), "Pages/board/Page.axaml", "<StackPanel>"
            + "<ListBox ItemsSource=\"{Binding Cards}\"><ListBox.ItemTemplate><DataTemplate x:DataType=\"vm:Card\"><v:PlainItem /></DataTemplate></ListBox.ItemTemplate></ListBox>"
            + List("<v:CardView a:View.ItemKey=\"Id\" />")
            + List("<StackPanel><v:ItemView /></StackPanel>")
            + "</StackPanel>").Run();
        var page = Assert.Single(result.Pages(out var assembly));
        var board = Activator.CreateInstance(assembly.GetType("App.Models.BoardModel")!)!;
        var cards = ((System.Collections.IEnumerable)board.GetType().GetProperty("Cards")!.GetValue(board)!).Cast<object>().ToList();
        var (keyed, items) = (page.Views[0], page.Views[1]);
        var services = new Services(Activator.CreateInstance(assembly.GetType("App.Loaders.CardViewLoader")!)!, Activator.CreateInstance(assembly.GetType("App.Loaders.ItemViewLoader")!)!);
        var token = TestContext.Current.CancellationToken;

        var loaded = await keyed.View.Loader!.LoadManyAsync!(services, ["a", "gone"], PageRequest.For("/board"), token);
        var byItem = await items.View.Loader!.LoadManyAsync!(services, cards, PageRequest.For("/board"), token);
        var model = keyed.View.Loader.CreateModel(services, loaded[0].Data)!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, page.Views.Length);
        Assert.Equal(("Pages/board/Page.axaml:0.1.0.0.0", 1, "Views/CardView.axaml"), (keyed.Key, keyed.List!.List, keyed.View.Source));
        Assert.Equal(("Pages/board/Page.axaml:0.2.0.0.0.0", 2, "Views/ItemView.axaml"), (items.Key, items.List!.List, items.View.Source));
        Assert.Equal(cards, keyed.List.Items(board)!.Cast<object>());
        Assert.Equal("a", keyed.List.Key!(cards[0]));
        Assert.Equal("text", keyed.List.Parse!("text"));
        Assert.Null(items.List.Key);
        Assert.Null(items.List.Parse);
        Assert.Equal(1, loaded[0].Data!.GetType().GetProperty("Comments")!.GetValue(loaded[0].Data));
        Assert.True(loaded[1].NotFound);
        Assert.Equal([5, 6], byItem.Select(outcome => (int)outcome.Data!.GetType().GetProperty("Comments")!.GetValue(outcome.Data)!));
        Assert.Same(loaded[0].Data, model.GetType().GetProperty("Data")!.GetValue(model));
    }

    [Fact]
    [Trait("AC", "GENERATOR-47")]
    public void AViewWithALoaderForOneInstanceInAListIsAWarning()
    {
        var result = Placed(App(), "Pages/board/Page.axaml", List("<v:SingleView />")).Run();
        var view = Assert.Single(Assert.Single(result.Pages(out _)).Views);
        var warning = Assert.Single(result.Diagnostics);

        Assert.Equal(("AIOROS020", DiagnosticSeverity.Warning), (warning.Id, warning.Severity));
        Assert.Equal(
            "Pages/board/Page.axaml: the view 'Views/SingleView.axaml' at 0.0.0.0 is placed in the item template of the list at 0 and its loader loads one instance, so it runs once per item (N+1 loads); let the list supply the item (remove the loader: the bound item is the view's model) or load every item at once with an IBatchLoader<TKey, TData>",
            warning.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("/app/Pages/board/Page.axaml", warning.Location.GetLineSpan().Path);
        Assert.NotNull(view.List);
        Assert.Null(view.View.Loader!.LoadManyAsync);
    }

    [Fact]
    [Trait("AC", "GENERATOR-48")]
    public void ViewsThatCannotRepeatAreBuildErrors()
    {
        var cases = new[]
        {
            "<v:CardView />",
            List("<v:CardView a:View.ItemKey=\"Id\" />", "Missing"),
            List("<v:CardView a:View.ItemKey=\"Nope\" />"),
            List("<v:CardView />"),
            List("<v:ItemView a:View.ItemKey=\"Id\" />"),
            "<ItemsControl ItemsSource=\"{Binding Columns}\"><ItemsControl.ItemTemplate><DataTemplate x:DataType=\"vm:Column\">" + List("<v:CardView a:View.ItemKey=\"Id\" />") + "</DataTemplate></ItemsControl.ItemTemplate></ItemsControl>",
        }.Select(body => Placed(App(), "Pages/board/Page.axaml", body).Run()).ToList();

        Assert.All(cases, result => Assert.Equal(("AIOROS021", DiagnosticSeverity.Error), (Assert.Single(result.Diagnostics).Id, result.Diagnostics[0].Severity)));
        Assert.All(cases, result => Assert.Empty(Assert.Single(result.Pages(out _)).Views));
        Assert.Equal(
            [
                "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0 cannot repeat: it has a batch loader but is not placed in a list's item template",
                "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0.0.0.0 cannot repeat: its list's ItemsSource '{Binding Missing}' is not a path of 'App.Models.BoardModel'",
                "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0.0.0.0 cannot repeat: its item key 'Nope' is not a path of 'App.Models.Card'",
                "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0.0.0.0 cannot repeat: its batch loader takes keys of 'string', but without a:View.ItemKey its items are its keys, of 'App.Models.Card'",
                "Pages/board/Page.axaml: the view 'Views/ItemView.axaml' at 0.0.0.0 cannot repeat: its batch loader takes keys of 'App.Models.Card', but its item key 'Id' is of 'string'",
                "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0.0.0.0.0.0.0 cannot repeat: it is placed in a list nested inside another list's item template",
            ],
            cases.Select(result => result.Diagnostics[0].GetMessage(CultureInfo.InvariantCulture)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-49")]
    public void ViewsChosenAtRunTimeAreNoted()
    {
        var result = Placed(App(), "Pages/board/Page.axaml", "<StackPanel>"
            + "<StackPanel.Resources><DataTemplate x:Key=\"card\"><v:SingleView /></DataTemplate></StackPanel.Resources>"
            + "<ContentControl><ContentControl.ContentTemplate><DataTemplate><v:CardView /></DataTemplate></ContentControl.ContentTemplate></ContentControl>"
            + "<ItemsControl><ItemsControl.DataTemplates><DataTemplate x:DataType=\"vm:Card\"><v:PlainItem /></DataTemplate></ItemsControl.DataTemplates></ItemsControl>"
            + "</StackPanel>")
            .Source("""
                namespace Avalonia.Controls.Templates { public interface IDataTemplate { } }
                namespace App { public sealed class ViewLocator : Avalonia.Controls.Templates.IDataTemplate { } }
                """, "ViewLocator.cs")
            .Run();
        var notes = result.Diagnostics.Select(diagnostic => (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(CultureInfo.InvariantCulture))).ToList();

        Assert.Empty(Assert.Single(result.Pages(out _)).Views);
        Assert.Equal(
            [
                ("AIOROS022", DiagnosticSeverity.Info, "'App.ViewLocator' chooses views at run time: the views it creates are not composed with their page, and their loaders do not run with the page's"),
                ("AIOROS022", DiagnosticSeverity.Info, "Pages/board/Page.axaml: the view 'Views/SingleView.axaml' at 0.0.0.0 is placed in a template chosen at run time (StackPanel.Resources): it is not composed with the page, and its loader does not run with the page's"),
                ("AIOROS022", DiagnosticSeverity.Info, "Pages/board/Page.axaml: the view 'Views/CardView.axaml' at 0.1.0.0.0 is placed in a template chosen at run time (ContentControl.ContentTemplate): it is not composed with the page, and its loader does not run with the page's"),
            ],
            notes);
    }

    [Fact]
    [Trait("AC", "GENERATOR-80")]
    public void AViewWithItsOwnDataContextIsNoInstance()
    {
        var result = Placed(App(), "Pages/board/Page.axaml", "<StackPanel><v:PlainItem DataContext=\"{Binding Cards[0]}\" /><v:SingleView /></StackPanel>").Run();

        var placed = Assert.Single(Assert.Single(result.Pages(out _)).Views);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(("Pages/board/Page.axaml:0.1", "Views/SingleView.axaml"), (placed.Key, placed.View.Source));
    }

    private static GeneratorHarness Placed(GeneratorHarness harness, string path, string body) =>
        harness.Xaml(path, body, "BoardModel", Views);
}
