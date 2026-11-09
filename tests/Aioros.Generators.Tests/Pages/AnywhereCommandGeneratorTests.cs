namespace Aioros.Generators.Tests.Pages;

public sealed class AnywhereCommandGeneratorTests
{
    private const string Avalonia = """
        namespace Avalonia
        {
            public class AvaloniaObject { }
        }

        namespace Avalonia.Media
        {
            public struct Color { public static Color Parse(string text) => default; }
            public sealed class SolidColorBrush : Avalonia.AvaloniaObject { public SolidColorBrush(Color color) { } }
        }

        namespace Avalonia.Threading
        {
            public sealed class Dispatcher
            {
                public static Dispatcher UIThread { get; } = new();
                public void Post(System.Action action) { }
            }
        }
        """;

    private const string Markup = """
        namespace Aioros.Markup
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = false)]
            public sealed class AnyThreadAttribute : System.Attribute { public static Avalonia.AvaloniaObject? Owner => null; }
        }
        """;

    private const string Toolkit = """
        namespace CommunityToolkit.Mvvm.Input
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class RelayCommandAttribute : System.Attribute { }

            public sealed class RelayCommand(System.Action execute) : System.Windows.Input.ICommand
            {
                public event System.EventHandler? CanExecuteChanged { add { } remove { } }
                public bool CanExecute(object? parameter) => true;
                public void Execute(object? parameter) => execute();
            }
        }
        """;

    private const string Tables = """
        namespace Cosmo
        {
            [Aioros.Markup.AnyThread]
            public sealed class TableSource<T>
            {
                public TableSource() => NextPageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => Page++);
                public int Page { get; set; }
                public System.Windows.Input.ICommand NextPageCommand { get; }
            }

            public sealed class Overlay
            {
                public static void Show(string text) => _ = new Avalonia.Media.SolidColorBrush(default);
                public System.Windows.Input.ICommand CloseCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
            }
        }
        """;

    private const string Models = """
        using System.Collections.Generic;
        using System.Linq;
        using System.Windows.Input;
        using CommunityToolkit.Mvvm.Input;

        namespace App.Models
        {
            public sealed class Row
            {
                public string Name { get; set; } = "";
            }

            public sealed record Entry(string Title)
            {
                public string Key { get; } = Title.ToUpperInvariant();
            }

            public sealed class Tinted(string name)
            {
                public object Fill { get; } = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(name));
            }

            public interface IStore
            {
                System.Threading.Tasks.Task SaveAsync(string title);
            }

            public sealed class Store : IStore
            {
                private readonly List<string> _saved = [];

                public System.Threading.Tasks.Task SaveAsync(string title)
                {
                    _saved.Add(title);
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }

            public interface IPainter
            {
                void Paint();
            }

            public sealed class Painter : IPainter
            {
                public void Paint() => _ = new Avalonia.Media.SolidColorBrush(default);
            }

            public interface IRemote
            {
                void Call();
            }

            public sealed partial class BoardModel
            {
                private readonly List<Row> _rows = [new Row()];
                private readonly IStore _store = new Store();
                private readonly IPainter _painter = new Painter();
                private readonly IRemote? _remote = null;

                public async System.Threading.Tasks.Task Persist() => await _store.SaveAsync(new Entry(Title).Key + new Entry(Title).Title);

                public void Shade() => _ = new Tinted(Title);

                public void Brush() => _painter.Paint();

                public void Reach() => _remote?.Call();

                public BoardModel()
                {
                    Rename = new RelayCommand(() => Retitle("renamed"));
                    Paint = new RelayCommand(() => _ = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#fff")));
                }

                public string Title { get; set; } = "";
                public Cosmo.TableSource<Row> Sections { get; } = new();
                public Cosmo.Overlay Dialog { get; } = new();
                public ICommand Rename { get; }
                public ICommand Paint { get; }
                public ICommand Clear { get; } = new RelayCommand(() => { });
                public ICommand Later { get; } = new RelayCommand(() => Avalonia.Threading.Dispatcher.UIThread.Post(() => { }));
                public ICommand Notify { get; } = new RelayCommand(() => Cosmo.Overlay.Show("saved"));

                [RelayCommand]
                private void Move() => Title = string.Join(",", _rows.Select(row => row.Name));

                public void View(string name) => Retitle(name);

                private System.Threading.Tasks.TaskCompletionSource<string>? _answer;

                public async System.Threading.Tasks.Task Ask()
                {
                    _answer = new System.Threading.Tasks.TaskCompletionSource<string>();
                    Title = await _answer.Task;
                }

                private System.Threading.Tasks.Task _pending = System.Threading.Tasks.Task.CompletedTask;

                public async System.Threading.Tasks.Task Wait() => await _pending;

                public async System.Threading.Tasks.Task Fetch() => await System.Threading.Tasks.Task.Yield();

                public void Tint() => _ = Avalonia.Media.Color.Parse("#000");

                private void Retitle(string title) => Title = title.Trim();
            }
        }
        """;

    private static GeneratorHarness Harness() => new GeneratorHarness()
        .Library("Avalonia.Base", Avalonia, Libraries.Framework)
        .Library("Aioros.Markup", Markup, Libraries.FrameworkAndLibraries)
        .Library("CommunityToolkit.Mvvm", Toolkit, Libraries.Framework)
        .Library("Cosmo", Tables, Libraries.FrameworkAndLibraries)
        .Source(Models)
        .Generated("namespace App.Models { public sealed partial class BoardModel { public System.Windows.Input.ICommand MoveCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(Move); } }");

    [Fact]
    [Trait("AC", "GENERATOR-120")]
    public void CommandsThatTouchNoAvaloniaAreListed()
    {
        var result = Harness()
            .Xaml("Pages/board/Page.axaml", """
                <StackPanel>
                  <Button Command="{Binding Rename}" />
                  <Button Command="{Binding Clear}" />
                  <Button Command="{Binding MoveCommand}" />
                  <Button Command="{Binding View}" CommandParameter="a" />
                  <Button Command="{Binding Tint}" />
                  <Button Command="{Binding Fetch}" />
                  <Button Command="{Binding Persist}" />
                  <Button Command="{Binding Sections.NextPageCommand}" />
                  <ItemsControl><ItemsControl.ItemTemplate><DataTemplate x:DataType="vm:BoardModel"><Button Command="{CompiledBinding Path=Clear}" /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </StackPanel>
                """, "BoardModel")
            .Run();
        var page = Assert.Single(result.Pages(out _), page => page.Route == "/board");

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(["Clear", "Fetch()", "MoveCommand", "Persist()", "Rename", "Sections.NextPageCommand", "Tint()", "View()"], page.AnywhereCommands);
    }

    [Fact]
    [Trait("AC", "GENERATOR-124")]
    public void PageCommandsReachedFromTemplatesAreListedWithTheirDepth()
    {
        var result = Harness()
            .Xaml("Pages/board/Page.axaml", """
                <ItemsControl>
                  <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:Row">
                      <StackPanel>
                        <Button Command="{Binding $parent[UserControl].((vm:BoardModel)DataContext).Clear}" />
                        <Button Command="{Binding $parent[UserControl].((vm:BoardModel)DataContext).Paint}" />
                        <Button Command="{Binding $parent[StackPanel].((vm:BoardModel)DataContext).Rename}" />
                        <Button Command="{Binding $parent[UserControl].((vm:Row)DataContext).Fetch}" />
                        <ItemsControl>
                          <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="vm:Row"><Button Command="{Binding $parent[UserControl].((vm:BoardModel)DataContext).MoveCommand}" /></DataTemplate>
                          </ItemsControl.ItemTemplate>
                        </ItemsControl>
                      </StackPanel>
                    </DataTemplate>
                  </ItemsControl.ItemTemplate>
                </ItemsControl>
                """, "BoardModel")
            .Run();
        var page = Assert.Single(result.Pages(out _), page => page.Route == "/board");

        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal(["^1.Clear", "^2.MoveCommand"], page.AnywhereCommands);
    }

    [Fact]
    [Trait("AC", "GENERATOR-121")]
    public void CommandsThatMayTouchAvaloniaAreNotListed()
    {
        var result = Harness()
            .Xaml("Pages/board/Page.axaml", """
                <StackPanel>
                  <Button Command="{Binding Paint}" />
                  <Button Command="{Binding Ask}" />
                  <Button Command="{Binding Brush}" />
                  <Button Command="{Binding Shade}" />
                  <Button Command="{Binding Reach}" />
                  <Button Command="{Binding Wait}" />
                  <Button Command="{Binding Later}" />
                  <Button Command="{Binding Notify}" />
                  <Button Command="{Binding Dialog.CloseCommand}" />
                  <Button Command="{Binding Clear, ElementName=Other}" />
                  <Button Command="{Binding $parent[TextBox].Paste}" />
                  <Button Command="{Binding Missing}" />
                  <StackPanel DataContext="{Binding Sections}"><Button Command="{Binding Clear}" /></StackPanel>
                  <ItemsControl><ItemsControl.ItemTemplate><DataTemplate><Button Command="{Binding Clear}" /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </StackPanel>
                """, "BoardModel")
            .Xaml("Pages/plain/Page.axaml", "<TextBlock />", "BoardModel")
            .Run();
        var pages = result.Pages(out _);

        Assert.Empty(result.WarningsAsErrors());
        Assert.Empty(Assert.Single(pages, page => page.Route == "/board").AnywhereCommands);
        Assert.Empty(Assert.Single(pages, page => page.Route == "/plain").AnywhereCommands);
    }
}
