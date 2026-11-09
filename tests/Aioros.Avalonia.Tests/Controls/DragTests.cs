using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Hosting;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Showcase.Board;
using Showcase.Models;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class DragTests(EngineFixture fixture)
{
    private const string Board = """
        <StackPanel>
          <ItemsControl ItemsSource="{Binding Columns}">
            <ItemsControl.ItemTemplate>
              <DataTemplate x:DataType="m:Column">
                <ItemsControl ItemsSource="{Binding Chips}" a:Drag.Target="{Binding Key}" a:Drag.Command="{Binding $parent[UserControl].((m:Sorting)DataContext).MoveCommand}">
                  <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="m:Chip"><Border a:Drag.Item="{Binding Id}" Background="Transparent"><TextBlock Text="{Binding Name}" /></Border></DataTemplate>
                  </ItemsControl.ItemTemplate>
                </ItemsControl>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
          <Button Content="Save" Command="{Binding SaveCommand}" CommandParameter="note" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "DRAG-01")]
    public async Task DragDeclarationsMarkTargetsAndItemsAsync()
    {
        var page = await fixture.CompileAsync(Board, new Sorting());
        var other = new Sorting { Columns = [new Column("later", new Chip("z", "Zeta"), new Chip("y", "Eta"))] };
        var html = EngineFixture.Body(page, new Sorting());
        var reused = EngineFixture.Body(page, other);
        var fresh = EngineFixture.Body(await fixture.CompileAsync(Board, other), other);

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(["0", "1"], DropTokens().Matches(html).Select(match => match.Groups[1].Value));
        Assert.Matches("<div data-drag=\"a\" tabindex=\"0\" aria-roledescription=\"draggable\" class=\"[^\"]+\">.*?Alpha.*?<noscript><button type=\"submit\" name=\"_btn\" value=\"0\\|up\\|a\">Move up</button><button type=\"submit\" name=\"_btn\" value=\"0\\|down\\|a\">Move down</button></noscript></div>", html);
        Assert.Contains("value=\"1|down|d\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"2|note\"", html, StringComparison.Ordinal);
        Assert.Equal(fresh, reused);
        Assert.Contains("value=\"0|up|y\"", reused, StringComparison.Ordinal);
        Assert.Contains("value=\"1|note\"", reused, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "DRAG-02")]
    public async Task PostedMovesRunTheTargetsCommandAsync()
    {
        string[] posts = ["1|0|a", "0|down|a", "0|up|c"];
        var page = await fixture.CompileAsync(Board, new Sorting());
        var avalonia = new Sorting();
        var planned = new Sorting();

        foreach (var post in posts)
        {
            Assert.Equal(PostStatus.Rendered, (await fixture.PostAsync(Board, avalonia, ("_btn", post))).Status);
            Assert.Equal(PostStatus.Rendered, await PlanForms.ApplyAsync(page.Plan, [planned], Form(post), new AdapterSlotResolver()));
        }

        (string, string, int)[] expected = [("a", "done", 0), ("a", "todo", 1), ("c", "todo", 1)];
        Assert.Equal(expected, avalonia.Moves);
        Assert.Equal(expected, planned.Moves);
    }

    [Fact]
    [Trait("AC", "DRAG-03")]
    public async Task MovesOutsideTheListRunNothingAsync()
    {
        (string Post, PostStatus Status)[] posts = [("1|2|a", PostStatus.BadRequest), ("0|x|a", PostStatus.BadRequest), ("0|up|a", PostStatus.Rendered), ("2|0|a", PostStatus.Conflict)];
        var page = await fixture.CompileAsync(Board, new Sorting());
        var avalonia = new Sorting();
        var planned = new Sorting();

        var answers = new List<(PostStatus, PostStatus?)>();
        foreach (var (post, _) in posts)
        {
            answers.Add(((await fixture.PostAsync(Board, avalonia, ("_btn", post))).Status, await PlanForms.ApplyAsync(page.Plan, [planned], Form(post), new AdapterSlotResolver())));
        }

        Assert.Equal(posts.Select(post => (post.Status, (PostStatus?)post.Status)), answers);
        Assert.Empty(avalonia.Moves);
        Assert.Empty(planned.Moves);
        Assert.Empty(avalonia.Saved);
        Assert.Empty(planned.Saved);
    }

    [Fact]
    [Trait("AC", "DRAG-08")]
    public async Task DroppedItemsRunTheTargetsCommandAsync()
    {
        var (moves, raised) = await fixture.Host.InvokeAsync(() =>
        {
            var sorting = new Sorting();
            var root = new XamlSources(typeof(TestApp).Assembly).Parse(ControlHarness.Xaml(Board, nameof(Sorting)));
            root.DataContext = sorting;
            var window = new Window { Width = 400, Height = 600, Content = root };
            window.Show();
            window.UpdateLayout();
            window.CaptureRenderedFrame();
            var raised = new List<(string, string, int)>();
            window.AddHandler(Drag.MovedEvent, (_, args) => raised.Add(args.Move));
            var todo = root.GetVisualDescendants().OfType<ItemsControl>().First(Drag.IsTarget);
            var second = Drag.Items(todo)[1];
            var upper = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 4), window)!.Value;
            var save = root.GetVisualDescendants().OfType<Button>().Single();
            var onSave = save.TranslatePoint(new Point(save.Bounds.Width / 2, save.Bounds.Height / 2), window)!.Value;
            var item = new DataTransfer();
            item.Add(DataTransferItem.Create(Drag.Format, "d"));
            var text = new DataTransfer();
            text.Add(DataTransferItem.CreateText("d"));

            Drop(window, upper, item);
            Drop(window, upper, text);
            Drop(window, onSave, item);
            window.Close();
            return (sorting.Moves.ToList(), raised);
        }, TestContext.Current.CancellationToken);

        Assert.Equal([("d", "todo", 1)], moves);
        Assert.Equal([("d", "todo", 1)], raised);
    }

    [Fact]
    [Trait("AC", "DRAG-10")]
    public async Task TheKanbanMovesCardsBetweenLanesAsync()
    {
        var xaml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Samples", "kanban.axaml"), TestContext.Current.CancellationToken);
        var store = new BoardStore();
        var board = new BoardFunctions(store);
        KanbanModel Model() => new(KanbanData.Of(store.State, "xaml"), board);

        var page = await fixture.Engine.RenderXamlAsync(xaml, [Model()], "light", TestContext.Current.CancellationToken);
        var html = EngineFixture.Body(page, Model());
        var lanes = DropTokens().Matches(html).Select(match => match.Groups[1].Value).ToList();
        var posted = await fixture.Engine.PostXamlAsync(xaml, [Model()], ImmutableDictionary<string, string>.Empty.Add("_btn", lanes[1] + "|1|AIO-2"), [], "light", TestContext.Current.CancellationToken);

        var stepped = await PlanForms.ApplyAsync(page.Plan, [Model()], Form(lanes[0] + "|up|AIO-4"), new AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(4, lanes.Count);
        Assert.Matches("aria-label=\"In progress\" data-drop=\"" + lanes[1] + "\"", html);
        Assert.Matches("data-drag=\"AIO-2\" tabindex=\"0\" aria-roledescription=\"draggable\"", html);
        Assert.Equal(PostStatus.Rendered, posted.Status);
        Assert.Equal(["AIO-5", "AIO-2", "AIO-6", "AIO-7"], store.State.Lanes[1].Items.Select(item => item.Id));
        Assert.Equal(PostStatus.Rendered, stepped);
        Assert.Equal(["AIO-1", "AIO-4", "AIO-3"], store.State.Lanes[0].Items.Select(item => item.Id));
    }

    private static void Drop(Window window, Point point, IDataTransfer data)
    {
        foreach (var type in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
        {
            window.DragDrop(point, type, data, DragDropEffects.Move, RawInputModifiers.None);
        }
    }

    private static ImmutableDictionary<string, string> Form(string button) => ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, button);

    [GeneratedRegex("data-drop=\"([^\"]+)\"")]
    private static partial Regex DropTokens();
}
