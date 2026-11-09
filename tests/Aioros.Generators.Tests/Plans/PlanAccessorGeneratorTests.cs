using System.Collections.Immutable;
using System.Reflection;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Generators.Tests.Plans;

public sealed class PlanAccessorGeneratorTests
{
    private const string Host = """
        namespace Aioros.Hosting
        {
            public sealed record AiorosApp(
                System.Collections.Immutable.ImmutableArray<Aioros.Pages.PageDefinition> Pages, System.Func<System.Type>? ApplicationType, System.Reflection.Assembly Assembly,
                string ProjectDirectory, Aioros.Pages.PageDefinition? NotFound = null, Aioros.Pages.PageDefinition? Error = null)
            {
                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Functions.ServerFunctionGroup> Functions { get; init; } = [];

                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Pages.NavigationInterface> Navigators { get; init; } = [];

                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Rendering.MemberAccessor> Accessors { get; init; } = [];
            }

            public static class AiorosHost
            {
                public static AiorosApp CreateBuilder(string[] args, AiorosApp app) => app;
            }
        }
        """;

    private const string Toolkit = """
        namespace CommunityToolkit.Mvvm.ComponentModel
        {
            [System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class ObservablePropertyAttribute : System.Attribute { }
        }

        namespace CommunityToolkit.Mvvm.Input
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class RelayCommandAttribute : System.Attribute { }
        }
        """;

    private const string Models = """
        using System.Collections.Generic;
        using System.Threading.Tasks;

        namespace App.Models;

        public sealed partial class BoardModel
        {
            public string Title { get; set; } = "Board";
            public List<Card> Cards { get; } = [new Card("a")];
            public Lane[] Lanes { get; init; } = [new Lane()];
            public int? Limit { get; set; } = 3;
            public IShape Shape { get; } = new Circle();
            public Tile Corner = new() { X = 4 };
            [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _owner = "ann";
            [CommunityToolkit.Mvvm.Input.RelayCommand] private Task SaveAsync() => Task.CompletedTask;
            [CommunityToolkit.Mvvm.Input.RelayCommand] private void OnArchive() { }
        }

        public sealed record Card(string Id) { public Person? Owner { get; init; } = new(); }
        public sealed class Lane { public string Name { get; set; } = "todo"; }
        public struct Tile { public int X { get; set; } }
        public interface IShape { double Area { get; } }
        public sealed class Circle : IShape { public double Area => 3.0; public string Kind => "circle"; }
        public abstract class Base { public string Label { get; set; } = "base"; }
        public sealed class Item : Base { public int Rank { get; set; } = 2; }
        public sealed class Person { public string Name { get; set; } = "p"; }
        public sealed class Note { public string Text { get; set; } = "n"; }
        public sealed class Unused { public int Count { get; set; } }
        """;

    private const string ToolkitOutput = """
        namespace App.Models;

        public sealed partial class BoardModel
        {
            public string Owner { get => _owner; set => _owner = value; }
            public object SaveCommand => "save";
            public object ArchiveCommand => "archive";
        }
        """;

    private const string Board = """
        <StackPanel>
          <ItemsControl><ItemsControl.ItemTemplate><DataTemplate x:DataType="vm:Item"><TextBlock /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
          <ContentControl><ContentControl.ContentTemplate><DataTemplate DataType="{x:Type vm:Note}"><TextBlock /></DataTemplate></ContentControl.ContentTemplate></ContentControl>
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "GENERATOR-51")]
    public void TypesNamedInXamlAndReachedFromThemGetTypedAccessors()
    {
        var result = new GeneratorHarness()
            .Source(Host).Source(Toolkit).Source(Models)
            .Xaml("Pages/Page.axaml", Board, "BoardModel")
            .Generated(ToolkitOutput)
            .Run();
        var assembly = result.Load();
        var all = (ImmutableArray<MemberAccessor>)assembly.GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("All")!.GetValue(null)!;
        var accessors = all.ToDictionary(accessor => (Owner(accessor.Owner), accessor.Name));
        var board = Activator.CreateInstance(assembly.GetType("App.Models.BoardModel")!)!;
        var item = Activator.CreateInstance(assembly.GetType("App.Models.Item")!)!;
        var app = assembly.GetType("AiorosApplication")!.GetMethod("CreateBuilder")!.Invoke(null, [Array.Empty<string>()])!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            ["BoardModel", "Card", "Circle", "Item", "Lane", "Lane[]", "List`1", "Note", "Person", "String", "Tile"],
            all.Select(accessor => Owner(accessor.Owner)).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ["ArchiveCommand", "Cards", "Corner", "Lanes", "Limit", "Owner", "SaveCommand", "Shape", "Title"],
            all.Where(accessor => accessor.Owner.Name == "BoardModel").Select(accessor => accessor.Name));
        Assert.Equal("Board", accessors[("BoardModel", "Title")].Get(board));
        accessors[("BoardModel", "Title")].Set!(board, "Plan");
        accessors[("BoardModel", "Limit")].Set!(board, null);
        accessors[("BoardModel", "Owner")].Set!(board, "bob");
        Assert.Equal(("Plan", null, "bob"), (accessors[("BoardModel", "Title")].Get(board), accessors[("BoardModel", "Limit")].Get(board), accessors[("BoardModel", "Owner")].Get(board)));
        Assert.Equal((typeof(string), typeof(int?), typeof(string)), (accessors[("BoardModel", "Title")].ValueType!(), accessors[("BoardModel", "Limit")].ValueType!(), accessors[("BoardModel", "Owner")].ValueType!()));
        Assert.Equal(("save", "archive"), (accessors[("BoardModel", "SaveCommand")].Get(board), accessors[("BoardModel", "ArchiveCommand")].Get(board)));
        Assert.Null(accessors[("BoardModel", "Lanes")].Set);
        Assert.Null(accessors[("BoardModel", "Cards")].Set);
        Assert.Null(accessors[("Card", "Owner")].Set);
        Assert.Null(accessors[("Tile", "X")].Set);
        Assert.Equal(4, accessors[("Tile", "X")].Get(accessors[("BoardModel", "Corner")].Get(board)!));
        Assert.Equal(1, accessors[("Lane[]", "Length")].Get(accessors[("BoardModel", "Lanes")].Get(board)!));
        Assert.Equal(1, accessors[("List`1", "Count")].Get(accessors[("BoardModel", "Cards")].Get(board)!));
        Assert.Equal("circle", accessors[("Circle", "Kind")].Get(accessors[("BoardModel", "Shape")].Get(board)!));
        Assert.Equal(("base", 2), (accessors[("Item", "Label")].Get(item), accessors[("Item", "Rank")].Get(item)));
        Assert.Contains(("Note", "Text"), accessors.Keys);
        Assert.Contains(("Person", "Name"), accessors.Keys);
        Assert.Equal(all, (ImmutableArray<MemberAccessor>)app.GetType().GetProperty("Accessors")!.GetValue(app)!);
    }

    [Fact]
    [Trait("AC", "GENERATOR-52")]
    public void MembersATypedAccessorCannotServeKeepTheRuntimeDelegate()
    {
        const string Unreachable = """
            using System;

            namespace App.Models;

            public sealed class OddModel
            {
                public static int Shared { get; set; }
                public int this[int index] => index;
                public Span<int> Window => default;
                public int Sink { set { } }
                public string Fixed { get; init; } = "f";
                public int Guarded { private get; set; }
                [Obsolete("old")] public int Old { get; set; }
                public Spot Place { get; set; }
                public IHidden Hidden { get; } = Outer.Make();
                public const int Limit = 3;
                public readonly int Seed = 1;
                public (string Card, int Index) Move { get; set; }
            }

            public class Parent { public int Twice { get; set; } }
            public sealed class Child : Parent { public new string Twice { get; set; } = string.Empty; public int Own { get; set; } }

            public struct Spot { public int X { get; set; } }

            public interface IHidden { int Depth { get; } }

            public static class Outer
            {
                public static IHidden Make() => new Secret();

                private sealed class Secret : IHidden { public int Depth => 1; }
            }
            """;

        var result = new GeneratorHarness()
            .Source(Unreachable)
            .Xaml("Pages/Page.axaml", "<ItemsControl><ItemsControl.ItemTemplate><DataTemplate x:DataType=\"vm:Child\"><TextBlock /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>", "OddModel")
            .Run();
        var all = (ImmutableArray<MemberAccessor>)result.Load().GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("All")!.GetValue(null)!;
        var names = all.Select(accessor => (Owner(accessor.Owner), accessor.Name, accessor.Set is not null)).ToList();

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(
            [("Child", "Own", true), ("OddModel", "Fixed", false), ("OddModel", "Hidden", false), ("OddModel", "Move", true), ("OddModel", "Place", true), ("OddModel", "Seed", false), ("Spot", "X", false), ("String", "Length", false)],
            names.Order());
    }

    [Fact]
    [Trait("AC", "GENERATOR-66")]
    public void AppTypesInAFrameworkNamedNamespaceAreWalked()
    {
        const string Samples = """
            namespace ReactiveUI.Samples.Suspension.ViewModels
            {
                public sealed class LoginViewModel
                {
                    public string? Username { get; set; } = "ann";
                    public Avalonia.Demo.Session Session { get; } = new();
                }
            }

            namespace Avalonia.Demo
            {
                public sealed class Session { public string Token { get; set; } = "t"; }
            }
            """;

        var result = new GeneratorHarness()
            .Source(Samples)
            .Xaml("Pages/Page.axaml", "<TextBox Text=\"{Binding Username}\" />", extra: " xmlns:rx=\"using:ReactiveUI.Samples.Suspension.ViewModels\" x:DataType=\"rx:LoginViewModel\"")
            .Run();
        var assembly = result.Load();
        var accessors = Accessors(assembly).ToDictionary(accessor => (Owner(accessor.Owner), accessor.Name));
        var login = Activator.CreateInstance(assembly.GetType("ReactiveUI.Samples.Suspension.ViewModels.LoginViewModel")!)!;

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal([("LoginViewModel", "Session"), ("LoginViewModel", "Username"), ("Session", "Token")], accessors.Keys.Where(key => key.Item1 != "String").Order());
        accessors[("LoginViewModel", "Username")].Set!(login, "bob");
        Assert.Equal("bob", accessors[("LoginViewModel", "Username")].Get(login));
        Assert.Equal("t", accessors[("Session", "Token")].Get(accessors[("LoginViewModel", "Session")].Get(login)!));
    }

    [Fact]
    [Trait("AC", "GENERATOR-67")]
    public void ReferencedFrameworkTypesThePathsReachGetTypedAccessors()
    {
        const string Framework = """
            namespace ReactiveUI
            {
                public class RoutingState
                {
                    public string NavigateBack { get; } = "back";
                    public int Depth { get; set; } = 2;
                    public Helper Inner { get; } = new();
                }

                public sealed class Helper { public int Deep => 3; }
                public sealed class Unread { public int Count => 1; }
                public sealed class Named { public string Label { get; } = "named"; }
            }
            """;
        const string Shell = """
            namespace App.Models;

            public sealed class ShellModel
            {
                public ReactiveUI.RoutingState Router { get; set; } = new();
                public ReactiveUI.Unread Other { get; } = new();
            }
            """;
        const string Body = """
            <StackPanel>
              <Button Command="{Binding Router.NavigateBack}" />
              <TextBlock><TextBlock.Text><Binding Path="Router.Depth" /></TextBlock.Text></TextBlock>
              <ContentControl><ContentControl.ContentTemplate><DataTemplate x:DataType="rx:Named"><TextBlock /></DataTemplate></ContentControl.ContentTemplate></ContentControl>
            </StackPanel>
            """;

        var result = new GeneratorHarness()
            .Library("ReactiveUI", Framework)
            .Source(Shell)
            .Xaml("Pages/Page.axaml", Body, "ShellModel", " xmlns:rx=\"using:ReactiveUI\"")
            .Run();
        var assembly = result.Load();
        var all = Accessors(assembly);
        var accessors = all.ToDictionary(accessor => (Owner(accessor.Owner), accessor.Name));
        var router = accessors[("ShellModel", "Router")].Get(Activator.CreateInstance(assembly.GetType("App.Models.ShellModel")!)!)!;

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(["Named", "RoutingState", "ShellModel", "String"], all.Select(accessor => Owner(accessor.Owner)).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal([("Depth", true), ("Inner", false), ("NavigateBack", false)], all.Where(accessor => accessor.Owner.Name == "RoutingState").Select(accessor => (accessor.Name, accessor.Set is not null)));
        Assert.Equal("back", accessors[("RoutingState", "NavigateBack")].Get(router));
        accessors[("RoutingState", "Depth")].Set!(router, 5);
        Assert.Equal(5, accessors[("RoutingState", "Depth")].Get(router));
        Assert.Contains(("Named", "Label"), accessors.Keys);
    }

    [Fact]
    [Trait("AC", "GENERATOR-68")]
    public void MembersReactiveUIGeneratesAreNamedByItsRules()
    {
        const string Attributes = """
            namespace ReactiveUI.SourceGenerators
            {
                public enum AccessModifier { Public, Protected, Internal, Private, InternalProtected, PrivateProtected, Init }

                [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
                public sealed class ReactiveAttribute : System.Attribute { public AccessModifier SetModifier { get; set; } }

                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class ReactiveCommandAttribute : System.Attribute { }
            }
            """;
        const string Model = """
            using System.Threading.Tasks;
            using ReactiveUI.SourceGenerators;

            namespace App.Models;

            public sealed partial class RxModel
            {
                [Reactive] private string _first = "1";
                [Reactive] private string m_second = "2";
                [Reactive] private string __third = "3";
                [Reactive] private string fourth = "4";
                [Reactive(SetModifier = AccessModifier.Protected)] private string _guarded = "g";
                [ReactiveCommand] private void Save() { }
                [ReactiveCommand] private void OnSubmit() { }
                [ReactiveCommand] private Task LoadAsync() => Task.CompletedTask;
                [ReactiveCommand] private ValueTask PingAsync() => default;
            }
            """;
        const string Output = """
            namespace App.Models;

            public sealed partial class RxModel
            {
                public string First { get => _first; set => _first = value; }
                public string Second { get => m_second; set => m_second = value; }
                public string Third { get => __third; set => __third = value; }
                public string Fourth { get => fourth; set => fourth = value; }
                public string Guarded { get => _guarded; private set => _guarded = value; }
                public object SaveCommand => "save";
                public object OnSubmitCommand => "submit";
                public object LoadCommand => "load";
                public object PingAsyncCommand => "ping";
            }
            """;

        var result = new GeneratorHarness()
            .Source(Attributes).Source(Model)
            .Xaml("Pages/Page.axaml", "<TextBlock />", "RxModel")
            .Generated(Output)
            .Run();
        var assembly = result.Load();
        var all = Accessors(assembly).Where(accessor => accessor.Owner.Name == "RxModel").ToList();
        var model = Activator.CreateInstance(assembly.GetType("App.Models.RxModel")!)!;

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(
            [("First", true), ("Fourth", true), ("Guarded", false), ("LoadCommand", false), ("OnSubmitCommand", false), ("PingAsyncCommand", false), ("SaveCommand", false), ("Second", true), ("Third", true)],
            all.Select(accessor => (accessor.Name, accessor.Set is not null)));
        all.Single(accessor => accessor.Name == "Second").Set!(model, "two");
        Assert.Equal(["1", "4", "g", "load", "submit", "ping", "save", "two", "3"], all.Select(accessor => accessor.Get(model)));
    }

    [Fact]
    [Trait("AC", "GENERATOR-69")]
    public async Task MethodsBoundAsCommandsGetMethodCommandAccessorsAsync()
    {
        const string Model = """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            namespace App.Models;

            public sealed class Greeter
            {
                public string Name { get; set; } = "";
                public List<string> Calls { get; } = new();
                public void SayHello() => Calls.Add("hello " + Name);
                public bool CanSayHello(object parameter) => Name.Length > 0;
                public async Task ArchiveAsync(int days) { await Task.Yield(); Calls.Add("archived " + days); }
                public void Twice() { }
                public void Twice(int times) { }
            }
            """;

        var result = new GeneratorHarness()
            .Source(Model)
            .Xaml("Pages/Page.axaml", "<StackPanel><Button Command=\"{Binding SayHello}\" /><Button Command=\"{Binding ArchiveAsync}\" /><Button Command=\"{Binding Twice}\" /></StackPanel>", "Greeter")
            .Run();
        var assembly = result.Load();
        var commands = Accessors(assembly).Where(accessor => accessor.Owner.Name == "Greeter" && accessor.Name.EndsWith(')')).ToDictionary(accessor => accessor.Name);
        var greeter = Activator.CreateInstance(assembly.GetType("App.Models.Greeter")!)!;
        var hello = (Runtime.Slots.MethodCommand)commands["SayHello()"].Get(greeter)!;
        var archive = (Runtime.Slots.MethodCommand)commands["ArchiveAsync()"].Get(greeter)!;
        var refused = hello.CanExecute(null);
        greeter.GetType().GetProperty("Name")!.SetValue(greeter, "Ada");
        await hello.ExecuteAsync(null);
        await archive.ExecuteAsync("30");

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(["ArchiveAsync()", "SayHello()"], commands.Keys.Order());
        Assert.False(refused);
        Assert.True(hello.CanExecute(null));
        Assert.True(archive.CanExecute(null));
        Assert.Equal(["hello Ada", "archived 30"], (List<string>)greeter.GetType().GetProperty("Calls")!.GetValue(greeter)!);
    }

    [Fact]
    [Trait("AC", "GENERATOR-117")]
    public void FrameworkTypesReachedThroughAnIndexGetTypedAccessors()
    {
        const string Framework = """
            namespace ReactiveUI
            {
                public sealed class Frame { public string Label { get; } = "frame"; }
                public sealed class Router { public System.Collections.Generic.IReadOnlyList<Frame> Stack { get; } = [new Frame(), new Frame()]; }
            }
            """;
        const string Shell = """
            namespace App.Models;

            public sealed class ShellModel
            {
                public ReactiveUI.Router Router { get; } = new();
            }
            """;
        const string Body = """
            <StackPanel>
              <TextBlock Text="{Binding Router.Stack[1].Label}" />
              <TextBlock Text="{Binding Path=Router.Stack[0].Label}" />
            </StackPanel>
            """;

        var result = new GeneratorHarness()
            .Library("ReactiveUI", Framework)
            .Source(Shell)
            .Xaml("Pages/Page.axaml", Body, "ShellModel")
            .Run();
        var assembly = result.Load();
        var accessors = Accessors(assembly).ToDictionary(accessor => (Owner(accessor.Owner), accessor.Name));
        var router = accessors[("ShellModel", "Router")].Get(Activator.CreateInstance(assembly.GetType("App.Models.ShellModel")!)!)!;
        var frame = ((System.Collections.IEnumerable)accessors[("Router", "Stack")].Get(router)!).Cast<object>().First();

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal("frame", accessors[("Frame", "Label")].Get(frame));
        Assert.Contains(("Router", "Stack"), accessors.Keys);
    }

    [Fact]
    [Trait("AC", "GENERATOR-118")]
    public void ReadOnlyListsIndexedInXamlGetIndexAccessors()
    {
        const string Ranked = """
            using System.Collections;
            using System.Collections.Generic;

            namespace App.Models;

            public sealed class Entry { public string Name { get; set; } = "first"; }

            public sealed class Ranking : IReadOnlyList<Entry>
            {
                private readonly List<Entry> _entries = [];
                public Ranking() : this(1) { }
                public Ranking(int count) { for (var index = 0; index < count; index++) _entries.Add(new Entry()); }
                public int Count => _entries.Count;
                public Entry this[int index] => _entries[index];
                public IEnumerator<Entry> GetEnumerator() => _entries.GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public sealed class RankModel { public Ranking Ranking { get; } = new(); }
            """;

        var result = new GeneratorHarness()
            .Source(Ranked)
            .Xaml("Pages/Page.axaml", """<TextBlock Text="{Binding Ranking[0].Name}" />""", "RankModel")
            .Run();
        var assembly = result.Load();
        var accessors = Accessors(assembly).ToDictionary(accessor => (Owner(accessor.Owner), accessor.Name));
        var ranking = Activator.CreateInstance(assembly.GetType("App.Models.Ranking")!)!;

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal("first", accessors[("Entry", "Name")].Get(accessors[("Ranking", "[0]")].Get(ranking)!));
        Assert.Null(accessors[("Ranking", "[0]")].Get(Activator.CreateInstance(assembly.GetType("App.Models.Ranking")!, 0)!));
        Assert.DoesNotContain(("Ranking", "[1]"), accessors.Keys);
    }

    private static ImmutableArray<MemberAccessor> Accessors(Assembly assembly) =>
        (ImmutableArray<MemberAccessor>)assembly.GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("All")!.GetValue(null)!;

    private static string Owner(Type type) => type.Name;
}
