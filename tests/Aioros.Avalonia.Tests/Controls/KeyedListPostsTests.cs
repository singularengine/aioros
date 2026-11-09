using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class KeyedListPostsTests(EngineFixture fixture)
{
    private const string RowBody = """
        <ItemsControl ItemsSource="{Binding Rows}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="m:KeyedRow">
              <StackPanel>
                <TextBox Text="{Binding Text, Mode=TwoWay}" />
                <Button Content="Save" Command="{Binding SaveCommand}" />
              </StackPanel>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
        """;

    private const string Rows = $"""<StackPanel><TextBox Text="{"{"}Binding Label, Mode=TwoWay{"}"}" />{RowBody}</StackPanel>""";

    private const string Filtered = $"""<StackPanel><TextBox Text="{"{"}Binding Filter, Mode=TwoWay{"}"}" />{RowBody}</StackPanel>""";

    private const string Identities = """
        <StackPanel>
          <ItemsControl ItemsSource="{Binding Coded}" a:Web.ItemKey="Code">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:CodedItem"><TextBox Text="{Binding Text, Mode=TwoWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <ItemsControl ItemsSource="{Binding Coded}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:CodedItem"><TextBox Text="{Binding Text, Mode=TwoWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <ItemsControl ItemsSource="{Binding Names}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="sys:String"><TextBox Text="{Binding Length, Mode=OneWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <ItemsControl ItemsSource="{Binding Plain}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:PlainItem"><TextBox Text="{Binding Text, Mode=TwoWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
        """;

    private const string InList = """
        <ListBox ItemsSource="{Binding Rows}">
          <ListBox.ItemTemplate>
            <DataTemplate x:DataType="m:KeyedRow">
              <StackPanel>
                <TextBox Text="{Binding Text, Mode=TwoWay}" />
                <Button Content="Save" Command="{Binding SaveCommand}" />
              </StackPanel>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
        """;

    private const string InTree = """
        <TreeView ItemsSource="{Binding Roots}">
          <TreeView.ItemTemplate>
            <TreeDataTemplate x:DataType="m:KeyedBranch" ItemsSource="{Binding Children}">
              <TextBox Text="{Binding Text, Mode=TwoWay}" />
            </TreeDataTemplate>
          </TreeView.ItemTemplate>
        </TreeView>
        """;

    private const string InMenu = """
        <ItemsControl ItemsSource="{Binding Rows}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="m:KeyedRow">
              <Button Content="More">
                <Button.Flyout>
                  <MenuFlyout><MenuItem Header="Save" Command="{Binding SaveCommand}" /></MenuFlyout>
                </Button.Flyout>
              </Button>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
        """;

    private const string Boxes = """
        <StackPanel>
          <ItemsControl ItemsSource="{Binding Rows}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:KeyedRow"><CheckBox IsChecked="{Binding IsOpen, Mode=TwoWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <TextBox Text="{Binding Label, Mode=TwoWay}" />
        </StackPanel>
        """;

    private const string PlainBody = """
        <StackPanel>
          <ItemsControl ItemsSource="{Binding Rows}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:PlainRow"><TextBox Text="{Binding Text, Mode=TwoWay}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <TextBox Text="{Binding Label, Mode=TwoWay}" />
        </StackPanel>
        """;

    private const string Noted = """
        <ItemsControl ItemsSource="{Binding Rows}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="m:KeyedRow">
              <StackPanel>
                <TextBlock a:Web.State="Text" Text="{Binding Note, Mode=TwoWay}" />
                <TextBox Text="{Binding Text, Mode=TwoWay}" />
              </StackPanel>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
        """;

    private const string Stated = """
        <ItemsControl ItemsSource="{Binding Rows}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="m:KeyedRow">
              <Expander a:Web.State="IsExpanded" IsExpanded="{Binding IsOpen, Mode=OneWay}" Header="Row"><TextBlock Text="{Binding Text}" /></Expander>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
        """;

    [Fact]
    [Trait("AC", "ENGINE-400")]
    public async Task ItemsNameTheirFieldsAndButtonsByKeyAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Rows, new KeyedRows("a", "b"), new KeyedRows("b", "a", "c"));
        var two = EngineFixture.Body(page, new KeyedRows("a", "b"));
        var moved = EngineFixture.Body(page, new KeyedRows("b", "a"));
        var fields = Names(two);
        var buttons = Buttons(two);

        Assert.Equal("f0", fields[0]);
        Assert.Matches("^f1~[0-9a-f]{24}$", fields[1]);
        Assert.Matches("^0~[0-9a-f]{24}$", buttons[0]);
        Assert.NotEqual(Key(fields[1]), Key(fields[2]));
        Assert.Equal(Key(fields[2]), Key(Names(moved)[1]));
        Assert.Equal(Key(fields[1]), Key(buttons[0]));
    }

    [Fact]
    [Trait("AC", "ENGINE-430")]
    public async Task IdsChosenToCollideAreStillDifferentItemsAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Rows, new KeyedRows("40189"), new KeyedRows("797186"));
        var written = EngineFixture.Body(page, new KeyedRows("40189"));
        var (field, button, shape) = (Names(written)[1], Buttons(written)[0], Regex.Match(written, "name=\"_shape\" value=\"([^\"]*)\"").Groups[1].Value);
        var form = ImmutableDictionary<string, string>.Empty.Add(field, "typed").Add("_btn", button + "|").Add("_shape", shape);
        var (planned, engine) = (new KeyedRows("797186"), new KeyedRows("797186"));

        var status = await PlanForms.ApplyAsync(page.Plan, [planned], form, new AdapterSlotResolver());
        var outcome = await fixture.PostAsync(Rows, engine, [.. form.Select(static pair => (pair.Key, pair.Value))]);

        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (status, outcome.Status));
        Assert.Empty(planned.Saved);
        Assert.Empty(engine.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-401")]
    public async Task AnItemsIdentityComesFromTheListItsIdOrItselfAsync()
    {
        CodedItem[] coded = [new("1", "one")];
        var page = await fixture.PlansLikeAvaloniaAsync(Identities, new IdentityLists(coded, ["n"], [new("p")]));
        var other = Split(EngineFixture.Body(page, new IdentityLists([new("9", "one")], ["n"], [new("p")])));
        var again = Split(EngineFixture.Body(page, new IdentityLists([new("1", "two")], ["m"], [new("p")])));
        var (byCode, byId, byValue, none) = Split(EngineFixture.Body(page, new IdentityLists(coded, ["n"], [new("p")])));

        Assert.Equal(byCode, other.ByCode);
        Assert.NotEqual(byId, other.ById);
        Assert.NotEqual(byCode, again.ByCode);
        Assert.Equal(byId, again.ById);
        Assert.NotEqual(byValue, again.ByValue);
        Assert.Equal("f3", none);
    }

    [Fact]
    [Trait("AC", "ENGINE-402")]
    public async Task APostOfAnItemNoLongerThereBindsAndRunsNothingAsync()
    {
        var (fields, buttons) = await WrittenAsync("a", "b", "c");
        (string, string)[] gone = [(fields[2], "typed"), (PlanForms.ButtonField, buttons[1] + "|")];

        var removed = await BothAsync(Rows, gone, "a", "c");
        var shifted = await BothAsync(Rows, gone, "b", "c");

        AssertStale(removed);
        AssertStale(shifted);
    }

    [Fact]
    [Trait("AC", "ENGINE-403")]
    public async Task AReorderedListIsStaleAndTheSameListBindsItsOwnItemAsync()
    {
        var (fields, buttons) = await WrittenAsync("a", "b");
        (string, string)[] post = [(fields[2], "typed"), (PlanForms.ButtonField, buttons[1] + "|")];

        var reordered = await BothAsync(Rows, post, "b", "a");
        var inserted = await BothAsync(Rows, post, "x", "a", "b");
        var same = await BothAsync(Rows, post, "a", "b");

        AssertStale(reordered);
        AssertStale(inserted);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (same.Engine, same.Plan!.Value));
        Assert.Equal(["b=typed"], same.EngineModel.Saved);
        Assert.Equal(["b=typed"], same.PlanModel.Saved);
        Assert.Equal(["", "typed"], same.PlanModel.Rows.Select(row => row.Text));
    }

    [Fact]
    [Trait("AC", "ENGINE-404")]
    public async Task AListThatFollowsAPostedFieldIsLookedAtOnceThatFieldIsBoundAsync()
    {
        var html = await fixture.HtmlAsync(Filtered, new KeyedRows("a", "b") { Filter = "b" });
        var (fields, buttons) = (Names(html), Buttons(html));
        (string, string)[] post = [("f0", "b"), (fields[1], "typed"), (PlanForms.ButtonField, buttons[0] + "|")];

        var kept = await BothAsync(Filtered, post, "a", "b");
        var gone = await BothAsync(Filtered, post, "a");

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (kept.Engine, kept.Plan!.Value));
        Assert.Equal(["b=typed"], kept.EngineModel.Saved);
        Assert.Equal(["b=typed"], kept.PlanModel.Saved);
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (gone.Engine, gone.Plan!.Value));
        Assert.Empty(gone.EngineModel.Saved);
        Assert.Empty(gone.PlanModel.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-405")]
    public async Task TheFieldAPostNamesAsChangedIsNotBoundBeforeTheListIsLookedAtAsync()
    {
        var fields = Names(await fixture.HtmlAsync(Filtered, new KeyedRows("a", "b")));
        (string, string)[] post = [(PlanForms.ChangeField, "0"), ("f0", "b"), (fields[1], "one"), (fields[2], "two")];

        var both = await BothAsync(Filtered, post, "a", "b");

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (both.Engine, both.Plan!.Value));
        Assert.Equal(["b:one,two"], both.PlanModel.FilterHistory);
        Assert.Equal(["b:one,two"], both.EngineModel.FilterHistory);
    }

    [Fact]
    [Trait("AC", "ENGINE-406")]
    public async Task ABareTokenBindsByItsPositionAsync()
    {
        (string, string)[] post = [("f2", "typed"), (PlanForms.ButtonField, "1|")];

        var both = await BothAsync(Rows, post, "a", "b");

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (both.Engine, both.Plan!.Value));
        Assert.Equal(["b=typed"], both.EngineModel.Saved);
        Assert.Equal(["b=typed"], both.PlanModel.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-407")]
    public async Task AnItemsTravellingStateNamesItsItemAsync()
    {
        var states = StateName().Matches(await fixture.HtmlAsync(Stated, new KeyedRows("a", "b"))).Select(match => match.Value).ToList();

        var same = new KeyedRows("a", "b");
        var restored = await fixture.PostAsync(Stated, same, (states[1], "True"));
        var shifted = new KeyedRows("a");
        var stale = await fixture.PostAsync(Stated, shifted, (states[1], "True"));

        Assert.Equal(2, states.Count);
        Assert.Matches("^_s0~[0-9a-f]{24}$", states[1]);
        Assert.Equal(PostStatus.Rendered, restored.Status);
        Assert.Equal([false, true], OpenDetails().Matches(EngineFixture.Body(restored.Page!, same)).Select(static match => match.Groups[1].Value.Contains(" open", StringComparison.Ordinal)));
        Assert.Equal(PostStatus.Stale, stale.Status);
    }

    [Fact]
    [Trait("AC", "ENGINE-408")]
    public async Task FieldsInAListBoxsItemsNameTheirItemAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(InList, new KeyedRows("a", "b"), new KeyedRows("b", "a", "c"));
        var html = EngineFixture.Body(page, new KeyedRows("a", "b"));
        var (fields, buttons) = (Names(html), Buttons(html));
        var field = fields.First(name => name.Contains('~', StringComparison.Ordinal));
        (string, string)[] post = [(fields[^1], "typed"), (PlanForms.ButtonField, buttons[^1] + "|")];

        var removed = await PostBothAsync(InList, new KeyedRows("a", "b"), post, () => new KeyedRows("a"));
        var same = await PostBothAsync(InList, new KeyedRows("a", "b"), post, () => new KeyedRows("a", "b"));

        Assert.Matches("^f[0-9]+~[0-9a-f]{24}$", field);
        Assert.Equal("f0", fields[0]);
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (removed.Engine, removed.Plan!.Value));
        Assert.Empty(removed.PlanModel.Saved);
        Assert.Equal(["b=typed"], same.PlanModel.Saved);
        Assert.Equal(["b=typed"], same.EngineModel.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-409")]
    public async Task FieldsInATreesItemsNameTheirItemAsync()
    {
        static KeyedTree Tree() => new(new KeyedBranch("r", new KeyedBranch("x"), new KeyedBranch("y")));
        var page = await fixture.PlansLikeAvaloniaAsync(InTree, Tree(), new KeyedTree(new KeyedBranch("r", new KeyedBranch("x"))));
        var fields = Names(EngineFixture.Body(page, Tree()));
        (string, string)[] post = [(fields[2], "typed")];

        var removed = await PostBothAsync(InTree, Tree(), post, () => new KeyedTree(new KeyedBranch("r", new KeyedBranch("y"))));
        var same = await PostBothAsync(InTree, Tree(), post, Tree);

        Assert.All(fields, static name => Assert.Matches("^f[0-9]+~[0-9a-f]{24}$", name));
        Assert.Equal(3, fields.Select(Key).Distinct().Count());
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (removed.Engine, removed.Plan!.Value));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (same.Engine, same.Plan!.Value));
        Assert.Equal("typed", same.PlanModel.Roots[0].Children[1].Text);
        Assert.Equal("typed", same.EngineModel.Roots[0].Children[1].Text);
    }

    [Fact]
    [Trait("AC", "ENGINE-410")]
    public async Task AListWhoseShapeChangedRefusesAPostThatNamesNoneOfItsItemsAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Boxes, new KeyedRows("a", "b", "c"), new KeyedRows("x"));
        var html = EngineFixture.Body(page, new KeyedRows("a", "b", "c"));
        (string, string)[] post = [(ShapeOf(html).Name, ShapeOf(html).Value), ("f3", "typed")];

        var changed = await PostBothAsync(Boxes, new KeyedRows("a", "b", "c"), post, () => new KeyedRows("a", "c"));
        var same = await PostBothAsync(Boxes, new KeyedRows("a", "b", "c"), post, () => new KeyedRows("a", "b", "c"));

        Assert.Matches("^[0-9a-f]{8}\\.0$", ShapeOf(html).Value);
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (changed.Engine, changed.Plan!.Value));
        Assert.Equal(("", ""), (changed.EngineModel.Label, changed.PlanModel.Label));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (same.Engine, same.Plan!.Value));
        Assert.Equal(("typed", "typed"), (same.EngineModel.Label, same.PlanModel.Label));
    }

    [Fact]
    [Trait("AC", "ENGINE-411")]
    public async Task AListThatWasEmptyOnThePageIsAChangedShapeAsync()
    {
        var html = await fixture.HtmlAsync(Boxes, new KeyedRows());
        (string, string)[] post = [(ShapeOf(html).Name, ShapeOf(html).Value), ("f0", "typed")];

        var filled = await PostBothAsync(Boxes, new KeyedRows("a"), post, () => new KeyedRows("a", "b"));
        var empty = await PostBothAsync(Boxes, new KeyedRows("a"), post, () => new KeyedRows());

        Assert.EndsWith(".", ShapeOf(html).Value, StringComparison.Ordinal);
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (filled.Engine, filled.Plan!.Value));
        Assert.All(filled.PlanModel.Rows, static row => Assert.False(row.IsOpen));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (empty.Engine, empty.Plan!.Value));
        Assert.Equal(("typed", "typed"), (empty.EngineModel.Label, empty.PlanModel.Label));
    }

    [Fact]
    [Trait("AC", "ENGINE-412")]
    public async Task AListWithoutIdentityIsCountedAndWarnedOfAsync()
    {
        static PlainRows Rows(int count) => new([.. Enumerable.Range(0, count).Select(static _ => new PlainRow())]);
        var page = await fixture.PlansLikeAvaloniaAsync(PlainBody, Rows(2), Rows(3));
        var html = EngineFixture.Body(page, Rows(2));
        (string, string)[] post = [(ShapeOf(html).Name, ShapeOf(html).Value), ("f1", "typed"), ("f2", "below")];

        var shorter = await PostBothAsync(PlainBody, Rows(2), post, () => Rows(1));
        var replaced = await PostBothAsync(PlainBody, Rows(2), post, () => new PlainRows(new PlainRow(), new PlainRow { Text = "another row" }));

        Assert.Equal(["f0", "f1", "f2"], Names(html));
        Assert.Contains(page.Warnings, static warning => warning.StartsWith("AIOROS033", StringComparison.Ordinal) && warning.Contains("a:Web.ItemKey", StringComparison.Ordinal));
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (shorter.Engine, shorter.Plan!.Value));
        Assert.Equal(("", ""), (shorter.EngineModel.Label, shorter.PlanModel.Label));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (replaced.Engine, replaced.Plan!.Value));
        Assert.Equal("typed", replaced.PlanModel.Rows[1].Text);
    }

    [Fact]
    [Trait("AC", "ENGINE-413")]
    public async Task AKeyedItemsStateIsWrittenAndRestoredFromThePlanAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Noted, new KeyedRows("a", "b"), new KeyedRows("b", "a", "c"));
        var states = StateName().Matches(EngineFixture.Body(page, new KeyedRows("a", "b"))).Select(match => match.Value).ToList();
        (string, string)[] post = [(states[1], "kept")];

        var same = await PostBothAsync(Noted, new KeyedRows("a", "b"), post, () => new KeyedRows("a", "b"));
        var gone = await PostBothAsync(Noted, new KeyedRows("a", "b"), post, () => new KeyedRows("a"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(2, states.Count);
        Assert.Equal(["", "kept"], same.PlanModel.Rows.Select(row => row.Note));
        Assert.Equal(["", "kept"], same.EngineModel.Rows.Select(row => row.Note));
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (gone.Engine, gone.Plan!.Value));
    }

    [Fact]
    [Trait("AC", "ENGINE-414")]
    public async Task ARefusedPostLeavesTheModelsAsTheyWereAsync()
    {
        var html = await fixture.HtmlAsync(Filtered, new KeyedRows("a", "b") { Filter = "b" });
        var (fields, buttons) = (Names(html), Buttons(html));
        (string, string)[] filtered = [("f0", "b"), (fields[1], "typed"), (PlanForms.ButtonField, buttons[0] + "|")];
        var written = await fixture.HtmlAsync(Noted, new KeyedRows("a", "b"));
        var state = StateName().Matches(written)[0].Value;
        (string, string)[] noted = [(state, "kept"), (ShapeOf(written).Name, "00000000.0")];

        var gone = await PostBothAsync(Filtered, new KeyedRows("a", "b"), filtered, () => new KeyedRows("a"));
        var refused = await PostBothAsync(Noted, new KeyedRows("a", "b"), noted, () => new KeyedRows("a", "b"));

        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (gone.Engine, gone.Plan!.Value));
        Assert.Equal(("", ""), (gone.EngineModel.Filter, gone.PlanModel.Filter));
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (refused.Engine, refused.Plan!.Value));
        Assert.Equal("", refused.PlanModel.Rows[0].Note);
        Assert.Equal("", refused.EngineModel.Rows[0].Note);
    }

    [Fact]
    [Trait("AC", "ENGINE-415")]
    public async Task AChangedFieldBeforeAListThatFollowsItBindsAloneAsync()
    {
        var html = await fixture.HtmlAsync(Filtered, new KeyedRows("a", "b") { Filter = "a" });
        var fields = Names(html);
        (string, string)[] typed = [(ShapeOf(html).Name, ShapeOf(html).Value), (PlanForms.ChangeField, "0"), ("f0", "b"), (fields[1], "one")];
        var boxes = await fixture.HtmlAsync(Boxes, new KeyedRows("a", "b"));
        (string, string)[] below = [(ShapeOf(boxes).Name, ShapeOf(boxes).Value), (PlanForms.ChangeField, "2"), ("f2", "typed")];

        var filtered = await PostBothAsync(Filtered, new KeyedRows("a", "b"), typed, () => new KeyedRows("a", "b"));
        var shifted = await PostBothAsync(Boxes, new KeyedRows("a", "b"), below, () => new KeyedRows("a"));

        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (filtered.Engine, filtered.Plan!.Value));
        Assert.Equal(("b", "b"), (filtered.EngineModel.Filter, filtered.PlanModel.Filter));
        Assert.All(filtered.PlanModel.Rows.Concat(filtered.EngineModel.Rows), static row => Assert.Equal("", row.Text));
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (shifted.Engine, shifted.Plan!.Value));
    }

    [Fact]
    [Trait("AC", "ENGINE-416")]
    public async Task AMenuInAKeyedItemNamesItsItemAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(InMenu, new KeyedRows("a", "b"), new KeyedRows("b"));
        var buttons = Buttons(EngineFixture.Body(page, new KeyedRows("a", "b")));
        (string, string)[] post = [(PlanForms.ButtonField, buttons[1] + "|")];

        var gone = await PostBothAsync(InMenu, new KeyedRows("a", "b"), post, () => new KeyedRows("a"));
        var same = await PostBothAsync(InMenu, new KeyedRows("a", "b"), post, () => new KeyedRows("a", "b"));

        Assert.All(buttons, static token => Assert.Matches("^[0-9]+~[0-9a-f]{24}$", token));
        Assert.Equal(PostStatus.Stale, gone.Engine);
        Assert.Equal(["b="], same.EngineModel.Saved);
    }

    private async Task<(PostStatus Engine, PostStatus? Plan, T EngineModel, T PlanModel)> PostBothAsync<T>(string body, T compiled, (string, string)[] post, Func<T> fresh)
        where T : class
    {
        var page = await fixture.CompileAsync(body, compiled);
        Assert.True(page.Reusable, page.Failure);
        var engine = fresh();
        var planned = fresh();
        var posted = await fixture.PostAsync(body, engine, post);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], post.ToImmutableDictionary(field => field.Item1, field => field.Item2), new AdapterSlotResolver());
        return (posted.Status, applied, engine, planned);
    }

    private static (string Name, string Value) ShapeOf(string html)
    {
        var match = ShapeMark().Match(html);
        Assert.True(match.Success, "no shape mark");
        return (match.Groups[1].Value, match.Groups[2].Value);
    }

    private async Task<(List<string> Fields, List<string> Buttons)> WrittenAsync(params string[] ids)
    {
        var html = await fixture.HtmlAsync(Rows, new KeyedRows(ids));
        return (Names(html), Buttons(html));
    }

    private async Task<(PostStatus Engine, PostStatus? Plan, KeyedRows EngineModel, KeyedRows PlanModel)> BothAsync(string body, (string, string)[] post, params string[] ids)
    {
        var page = await fixture.CompileAsync(body, new KeyedRows("a", "b"));
        Assert.True(page.Reusable, page.Failure);
        var engine = new KeyedRows(ids);
        var planned = new KeyedRows(ids);
        var posted = await fixture.PostAsync(body, engine, post);
        var applied = await PlanForms.ApplyAsync(page.Plan, [planned], post.ToImmutableDictionary(field => field.Item1, field => field.Item2), new AdapterSlotResolver());
        return (posted.Status, applied, engine, planned);
    }

    private static void AssertStale((PostStatus Engine, PostStatus? Plan, KeyedRows EngineModel, KeyedRows PlanModel) answer)
    {
        Assert.Equal((PostStatus.Stale, PostStatus.Stale), (answer.Engine, answer.Plan!.Value));
        Assert.Empty(answer.EngineModel.Saved);
        Assert.Empty(answer.PlanModel.Saved);
        Assert.All(answer.EngineModel.Rows, row => Assert.Equal("", row.Text));
        Assert.All(answer.PlanModel.Rows, row => Assert.Equal("", row.Text));
    }

    private static (string ByCode, string ById, string ByValue, string None) Split(string html)
    {
        var names = Names(html);
        return (Key(names[0]), Key(names[1]), Key(names[2]), names[3]);
    }

    private static List<string> Names(string html) => [.. FieldName().Matches(html).Select(match => match.Groups[1].Value)];

    private static List<string> Buttons(string html) => [.. ButtonToken().Matches(html).Select(match => match.Groups[1].Value)];

    private static string Key(string token) => token[(token.IndexOf('~', StringComparison.Ordinal) + 1)..];

    [GeneratedRegex("name=\"(f[^\"]+)\"")]
    private static partial Regex FieldName();

    [GeneratedRegex("name=\"_btn\" value=\"([^|\"]+)\\|")]
    private static partial Regex ButtonToken();

    [GeneratedRegex("name=\"(_shape[^\"]*)\" value=\"([^\"]*)\"")]
    private static partial Regex ShapeMark();

    [GeneratedRegex("<details([^>]*)>")]
    private static partial Regex OpenDetails();

    [GeneratedRegex("_s[0-9]+~[0-9a-f]{24}")]
    private static partial Regex StateName();
}
