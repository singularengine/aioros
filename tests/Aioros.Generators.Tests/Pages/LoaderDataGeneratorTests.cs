using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class LoaderDataGeneratorTests
{
    private const string Everything = """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        namespace App.Models;
        public enum Mood { Calm, Busy = 5 }
        public sealed record Person(string Name, int Age);
        public sealed class Box { public string? Label { get; set; } public double Weight { get; init; } }
        public readonly record struct Point(int X, int Y);
        public sealed record Everything(
            string Id, string? Missing, int Count, long Big, double Ratio, decimal Price, bool Done, char Letter,
            Guid Key, DateTime At, DateTimeOffset Stamp, DateOnly Day, TimeOnly Time, TimeSpan Span,
            Mood Mood, int? Maybe, int? Nothing, Person Owner, Person? Nobody, Person[] People, List<string> Tags,
            IReadOnlyList<Person> Readers, ImmutableArray<int> Scores, Dictionary<string, Person> ByName, Point Where, Point? Somewhere)
        {
            public Box Box { get; init; } = new();
            public string Computed => Id + "!";
        }
        public sealed class EverythingModel(Everything data) { public Everything Data { get; } = data; }
        """;

    private const string Value = """
        new App.Models.Everything(
            "7", null, 3, 9_000_000_000, 0.25, 12.5m, true, 'z',
            new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"), new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Utc), new DateTimeOffset(2026, 10, 11, 8, 30, 0, TimeSpan.FromHours(2)),
            new DateOnly(2026, 10, 11), new TimeOnly(8, 30, 15), TimeSpan.FromMinutes(90),
            App.Models.Mood.Busy, 4, null, new App.Models.Person("Ada", 36), null, [new App.Models.Person("Grace", 45)], ["a", "b"],
            [new App.Models.Person("Linus", 30)], [1, 2], new() { ["ada"] = new App.Models.Person("Ada", 36) }, new App.Models.Point(1, 2), null)
        { Box = new App.Models.Box { Label = "b", Weight = 2.5 } }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-27")]
    public async Task LoaderDataIsWrittenAndReadByEmittedCodeAsync()
    {
        var result = new GeneratorHarness()
            .Source(Everything)
            .Source(Loader("EverythingLoader", "App.Models.Everything", Value), "Pages/Page.Loader.cs")
            .Xaml("Pages/Page.axaml", "<TextBlock />", "EverythingModel")
            .Run();
        var page = Assert.Single(result.Pages(out _));
        var data = (await page.Loader!.LoadAsync(new Services(), PageRequest.For("/"), TestContext.Current.CancellationToken)).Data;

        var json = Write(page.Loader, data);
        using var document = JsonDocument.Parse(json);
        var read = page.Loader.Read!(document.RootElement);

        Assert.Empty(result.Diagnostics);
        Assert.StartsWith("""{"id":"7","missing":null,"count":3,"big":9000000000,"ratio":0.25,"price":12.5,"done":true,"letter":"z",""", json, StringComparison.Ordinal);
        Assert.Contains("\"mood\":5,\"maybe\":4,\"nothing\":null,\"owner\":{\"name\":\"Ada\",\"age\":36},\"nobody\":null,", json, StringComparison.Ordinal);
        Assert.Contains("\"scores\":[1,2],\"byName\":{\"ada\":{\"name\":\"Ada\",\"age\":36}},\"where\":{\"x\":1,\"y\":2},\"somewhere\":null,\"box\":{\"label\":\"b\",\"weight\":2.5}}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("computed", json, StringComparison.Ordinal);
        Assert.Equal(data!.GetType(), read!.GetType());
        Assert.Equal(json, Write(page.Loader, read));
        Assert.Equal("7!", read.GetType().GetProperty("Computed")!.GetValue(read));
        var generated = string.Join("\n", result.Sources);
        Assert.Contains("Utf8JsonWriter", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", generated, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "GENERATOR-28")]
    public void TheDataContractHashesThePagesDataShapes()
    {
        static ImmutableArray<PageDefinition> Build(string issue, string shell) =>
            new GeneratorHarness()
                .Source($$"""
                    namespace App.Models;
                    public sealed record Person({{issue}});
                    public sealed record IssueData(string Id, Person Owner);
                    public sealed record ShellData({{shell}});
                    public sealed class IssueModel(IssueData data) { }
                    public sealed class ShellModel(ShellData data) { }
                    """)
                .Source(Loader("ShellLoader", "App.Models.ShellData", "default!"), "Pages/Layout.Loader.cs")
                .Source(Loader("IssueLoader", "App.Models.IssueData", "default!"), "Pages/issues/Page.Loader.cs")
                .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
                .Xaml("Pages/issues/Page.axaml", "<TextBlock />", "IssueModel")
                .Xaml("Pages/about/Page.axaml", "<TextBlock />")
                .Xaml("Pages/help/Page.axaml", "<TextBlock />")
                .Run().Pages(out _);
        static string Contract(ImmutableArray<PageDefinition> pages, string route) => pages.Single(page => page.Route == route).DataContract;

        var original = Build("string Name", "string User");
        var again = Build("string Name", "string User");
        var renamed = Build("string FullName", "string User");
        var retyped = Build("int Name", "string User");
        var layout = Build("string Name", "string User, int Unread");

        Assert.Matches("^[0-9a-f]{16}$", Contract(original, "/issues"));
        Assert.Equal(Contract(original, "/issues"), Contract(again, "/issues"));
        Assert.NotEqual(Contract(original, "/issues"), Contract(renamed, "/issues"));
        Assert.NotEqual(Contract(original, "/issues"), Contract(retyped, "/issues"));
        Assert.NotEqual(Contract(original, "/issues"), Contract(layout, "/issues"));
        Assert.NotEqual(Contract(original, "/about"), Contract(original, "/issues"));
        Assert.NotEqual(Contract(original, "/about"), Contract(layout, "/about"));
        Assert.Equal(Contract(Build("string Name", "string User"), "/help"), Contract(original, "/help"));
        Assert.Equal(Contract(new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Pages(out _), "/"), new GeneratorHarness().Xaml("Pages/x/Page.axaml", "<TextBlock />").Run().Pages(out _)[0].DataContract);
    }

    private const string Board = """
        using System.Collections.Generic;
        using System.Text.Json.Serialization;
        namespace App.Models;
        [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
        [JsonDerivedType(typeof(TaskItem), "task")]
        [JsonDerivedType(typeof(BugItem), "bug")]
        [JsonDerivedType(typeof(Regression), "regression")]
        public abstract record BoardItem(string Id);
        public sealed record TaskItem(string Id, string Area) : BoardItem(Id);
        public record BugItem(string Id, int Reports) : BoardItem(Id);
        public sealed record Regression(string Id, int Reports, string Since) : BugItem(Id, Reports);
        public sealed record Hotfix(string Id, int Reports, string Patch) : BugItem(Id, Reports);
        [JsonDerivedType(typeof(Circle), "circle")]
        public interface IShape { double Size { get; } }
        public sealed record Circle(double Size) : IShape;
        public sealed record Square(double Size) : IShape;
        [JsonDerivedType(typeof(Pinned), 1)]
        public class Note { public string Text { get; init; } = ""; }
        public sealed class Pinned : Note { public int Order { get; init; } }
        public sealed record Board(IReadOnlyList<BoardItem> Items, BoardItem? Focus, BoardItem? Nothing, IShape Shape, List<Note> Notes);
        public sealed class BoardModel(Board data) { public Board Data { get; } = data; }
        """;

    private const string BoardValue = """
        new App.Models.Board(
            [new App.Models.TaskItem("t1", "Docs"), new App.Models.BugItem("b1", 3), new App.Models.Regression("r1", 2, "1.2"), new App.Models.Hotfix("h1", 1, "p")],
            new App.Models.TaskItem("t2", "Web"), null, new App.Models.Circle(2.5),
            [new App.Models.Note { Text = "plain" }, new App.Models.Pinned { Text = "top", Order = 1 }])
        """;

    [Fact]
    [Trait("AC", "GENERATOR-33")]
    public async Task PolymorphicDataIsWrittenAndReadAsSystemTextJsonDoesAsync()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var result = BuildBoard(Board, BoardValue);
        var page = Assert.Single(result.Pages(out _));
        var data = (await page.Loader!.LoadAsync(new Services(), PageRequest.For("/"), TestContext.Current.CancellationToken)).Data!;
        var squareLoader = BuildBoard(Board, BoardValue.Replace("new App.Models.Circle(2.5)", "new App.Models.Square(1)", StringComparison.Ordinal)).Pages(out _)[0].Loader!;
        var square = (await squareLoader.LoadAsync(new Services(), PageRequest.For("/"), TestContext.Current.CancellationToken)).Data;

        var ours = Write(page.Loader, data);
        var theirs = Serialize(data, web);
        using var theirDocument = JsonDocument.Parse(theirs);
        var readFromTheirs = page.Loader.Read!(theirDocument.RootElement);
        var readByThem = JsonSerializer.Deserialize(ours, data.GetType(), web)!;
        using var shuffled = JsonDocument.Parse("""
            {"items":[{"id":"t9","area":"Ops","kind":"task"},{"since":"2.0","kind":"regression","id":"r9","reports":4}],"focus":null,"nothing":null,"shape":{"size":1,"$type":"circle"},"notes":[{"order":2,"text":"x","$type":1},{"text":"y"}]}
            """);
        var reordered = page.Loader.Read!(shuffled.RootElement)!;
        using var unknown = JsonDocument.Parse("""{"items":[{"kind":"epic","id":"e1"}],"focus":null,"nothing":null,"shape":{"$type":"circle","size":1},"notes":[]}""");
        using var untyped = JsonDocument.Parse("""{"items":[{"id":"e1"}],"focus":null,"nothing":null,"shape":{"$type":"circle","size":1},"notes":[]}""");

        Assert.Empty(result.Diagnostics);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(theirs), JsonNode.Parse(ours)), ours + "\n" + theirs);
        Assert.StartsWith("""{"items":[{"kind":"task","id":"t1","area":"Docs"},{"kind":"bug",""", ours, StringComparison.Ordinal);
        Assert.Contains("""{"kind":"bug","id":"h1","reports":1}""", ours, StringComparison.Ordinal);
        Assert.Contains("""{"text":"plain"},{"$type":1,""", ours, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(theirs), JsonNode.Parse(Write(page.Loader, readFromTheirs))));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(theirs), JsonNode.Parse(Serialize(readByThem, web))));
        Assert.Equal(["TaskItem", "Regression"], Items(reordered).Select(item => item!.GetType().Name));
        Assert.Equal(["Pinned", "Note"], ((System.Collections.IEnumerable)reordered.GetType().GetProperty("Notes")!.GetValue(reordered)!).Cast<object>().Select(note => note.GetType().Name));
        Assert.Throws<JsonException>(() => page.Loader.Read!(unknown.RootElement));
        Assert.Throws<JsonException>(() => page.Loader.Read!(untyped.RootElement));
        Assert.Throws<NotSupportedException>(() => Write(squareLoader, square));
        Assert.Throws<NotSupportedException>(() => Serialize(square!, web));
        Assert.NotEqual(page.DataContract, BuildBoard(Board.Replace("string Area", "string Team", StringComparison.Ordinal), BoardValue).Pages(out _)[0].DataContract);
        Assert.DoesNotContain("JsonSerializer", string.Join("\n", result.Sources), StringComparison.Ordinal);
        var unnamed = BuildBoard(Board.Replace("[JsonDerivedType(typeof(Circle), \"circle\")]", "[JsonDerivedType(typeof(Circle))]", StringComparison.Ordinal), BoardValue).Diagnostics;
        Assert.Contains("without a type discriminator", Assert.Single(unnamed, diagnostic => diagnostic.Id == "AIOROS011").GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "GENERATOR-34")]
    public void GeneratedCodeForNullableMembersCompilesWithoutWarnings()
    {
        var result = new GeneratorHarness()
            .Source("""
                using System.Collections.Generic;
                using System.Text.Json.Serialization;
                namespace App.Models;
                [JsonDerivedType(typeof(Leaf), "leaf")]
                public abstract record Node(string Name);
                public sealed record Leaf(string Name) : Node(Name);
                public sealed record Person(string Name);
                public sealed record Loose(
                    Person? Owner, List<Person>? People, Dictionary<string, Person?>? ByName, Person[]? Team, Node? Root,
                    List<string?> Tags, IReadOnlyList<Person?> Maybe, string? Note, int? Count, string?[] Labels);
                public sealed class LooseModel(Loose data) { public Loose Data { get; } = data; }
                """)
            .Source(Loader("LooseLoader", "App.Models.Loose", "default(App.Models.Loose)!"), "Pages/Page.Loader.cs")
            .Xaml("Pages/Page.axaml", "<TextBlock />", "LooseModel")
            .Run();

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.WarningsAsErrors());
    }

    private static string Serialize(object data, JsonSerializerOptions options) => JsonSerializer.Serialize(data, data.GetType(), options);

    private static GeneratorResult BuildBoard(string types, string value) =>
        new GeneratorHarness()
            .Source(types)
            .Source(Loader("BoardLoader", "App.Models.Board", value), "Pages/Page.Loader.cs")
            .Xaml("Pages/Page.axaml", "<TextBlock />", "BoardModel")
            .Run();

    private static IEnumerable<object?> Items(object board) =>
        ((System.Collections.IEnumerable)board.GetType().GetProperty("Items")!.GetValue(board)!).Cast<object?>();

    private static string Write(LoaderDefinition loader, object? data)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            loader.Write!(writer, data);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Loader(string name, string data, string value) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        public sealed class {{name}} : ILoader<{{data}}>
        {
            public ValueTask<LoaderResult<{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<{{data}}>>({{value}});
        }
        """;
}
