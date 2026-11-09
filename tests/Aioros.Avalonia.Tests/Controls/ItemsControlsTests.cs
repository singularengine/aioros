using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ItemsControlsTests(EngineFixture fixture)
{
    private const string Radio = "<input type=\"radio\" class=\"vh\"";

    private const string Check = "<input type=\"checkbox\" class=\"vh\"";

    [Fact]
    [Trait("AC", "ITEMS-147")]
    public async Task ComboBoxesWithABoundIndexAreAnsweredFromThePlanAsync()
    {
        const string body = """
            <StackPanel>
              <ComboBox SelectedIndex="{Binding Tab}"><ComboBoxItem Content="Months" /><ComboBoxItem Content="Days" /><ComboBoxItem Content="Hours" /></ComboBox>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(body, new Models.Showcase(), new Models.Showcase { Tab = 2 });
        var model = new Models.Showcase();

        var posted = await Aioros.Runtime.Rendering.PlanForms.ApplyAsync(page.Plan, [model], System.Collections.Immutable.ImmutableDictionary.CreateRange([KeyValuePair.Create("_chg", "0"), KeyValuePair.Create("f0", "1")]), new Plans.AdapterSlotResolver());

        Assert.True(Aioros.Runtime.Rendering.PlanForms.Fields(page.Plan, [new Models.Showcase()]).Described);
        Assert.Contains("<option value=\"2\" selected>", EngineFixture.Body(page, new Models.Showcase { Tab = 2 }), StringComparison.Ordinal);
        Assert.Equal(Aioros.Runtime.Rendering.PostStatus.Rendered, posted);
        Assert.Equal(1, model.Tab);
    }

    [Fact]
    [Trait("AC", "ITEMS-01")]
    public async Task ComboBoxesAreSelectsAsync()
    {
        var profile = new Profile();
        const string Body = """
            <StackPanel>
              <ComboBox SelectedItem="{Binding Plan}">
                <x:String>Free</x:String>
                <x:String>Pro</x:String>
                <x:String>Team &amp; more</x:String>
              </ComboBox>
              <ComboBox SelectedIndex="1">
                <ComboBoxItem>Small</ComboBoxItem>
                <ComboBoxItem>Large</ComboBoxItem>
              </ComboBox>
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body, profile);
        await fixture.PostAsync(Body, profile, ("f0", "2"));
        var bound = await fixture.CompileAsync("""<ComboBox ItemsSource="{Binding Choices}" SelectedItem="{Binding Choice}" />""", new Tiers());
        var other = EngineFixture.Body(bound, new Tiers { Choices = ["Basic", "Plus", "Max"], Choice = "Max" });

        Assert.True(bound.Reusable, bound.Failure);
        Assert.Matches("<option value=\"0\">Basic</option><option value=\"1\">Plus</option><option value=\"2\" selected>Max</option></select>", other);

        Assert.Matches("<select name=\"f0\"(?: data-aioros-local=\"\")? data-change class=\"[^\"]+\"><option value=\"0\">Free</option><option value=\"1\" selected>Pro</option><option value=\"2\">Team &amp; more</option></select>", html);
        Assert.Matches("<select name=\"f1\"(?: data-aioros-local=\"\")? class=\"[^\"]+\"><option value=\"0\">Small</option><option value=\"1\" selected>Large</option></select>", html);
        Assert.Equal("Team & more", profile.Plan);
    }

    [Fact]
    [Trait("AC", "ITEMS-02")]
    public async Task ListBoxesAreRadioGroupsAsync()
    {
        var basket = new Basket();
        const string Body = """
            <ListBox SelectedItem="{Binding Fruit}">
              <x:String>Apple</x:String>
              <x:String>Pear</x:String>
              <x:String>Plum</x:String>
            </ListBox>
            """;

        var page = await fixture.CompileAsync(Body, basket);
        var html = EngineFixture.Body(page, basket);
        await fixture.PostAsync(Body, basket, ("f0", "2"));

        Assert.Matches(
            $"<div role=\"radiogroup\" data-change class=\"c [^\"]+\"><label class=\"(?<item>[^\"]+)\">{Radio} name=\"f0\" value=\"0\">Apple</label>" +
            $"<label class=\"\\k<item>\">{Radio} name=\"f0\" value=\"1\" checked>Pear</label><label class=\"\\k<item>\">{Radio} name=\"f0\" value=\"2\">Plum</label></div>",
            html);
        Assert.Matches("\\.k[0-9a-f]+\\{position:relative;display:flex;flex-direction:column;cursor:pointer;padding:9px 12px 12px 12px;\\}", page.Css);
        Assert.Matches("\\.it[0-9a-f]+:has\\(>input:checked\\)\\{background:rgba\\(0,120,215,0\\.4\\);", page.Css);
        Assert.Equal("Plum", basket.Fruit);
    }

    [Fact]
    [Trait("AC", "ITEMS-03")]
    public async Task MultipleSelectionListBoxesAreCheckboxesAsync()
    {
        var basket = new Basket();
        const string Body = """
            <ListBox SelectionMode="Multiple">
              <ListBoxItem IsSelected="True">Apple</ListBoxItem>
              <ListBoxItem>Pear</ListBoxItem>
              <ListBoxItem IsSelected="True">Plum</ListBoxItem>
            </ListBox>
            """;

        var html = await fixture.HtmlAsync(Body, basket);
        var two = await fixture.PostAsync(Body, basket, ("f0", "1,2"));
        var none = await fixture.PostAsync(Body, basket);

        Assert.Matches($"<div role=\"group\" class=\"c [^\"]+\"><label class=\"[^\"]+\">{Check} name=\"f0\" value=\"0\" checked>Apple</label><label class=\"[^\"]+\">{Check} name=\"f0\" value=\"1\">Pear</label><label class=\"[^\"]+\">{Check} name=\"f0\" value=\"2\" checked>Plum</label>", html);
        Assert.Matches("value=\"0\">Apple.*value=\"1\" checked>Pear.*value=\"2\" checked>Plum", EngineFixture.Body(two.Page!, basket));
        Assert.DoesNotContain("checked", EngineFixture.Body(none.Page!, basket), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-04")]
    public async Task BoundListBoxesBecomeARepeatWithSelectionAsync()
    {
        var page = await fixture.CompileAsync(
            """
            <ListBox ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}">
              <ListBox.ItemTemplate>
                <DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate>
              </ListBox.ItemTemplate>
            </ListBox>
            """,
            new Shelf(0, new Item("only")));

        var html = EngineFixture.Body(page, new Shelf(2, new Item("a1"), new Item("b2"), new Item("c3")));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches(
            $"<label class=\"[^\"]+\">{Radio} name=\"f0\" value=\"0\"><div[^>]*>a1</div></label><label class=\"[^\"]+\">{Radio} name=\"f0\" value=\"1\"><div[^>]*>b2</div></label>" +
            $"<label class=\"[^\"]+\">{Radio} name=\"f0\" value=\"2\" checked><div[^>]*>c3</div></label>",
            html);
        Assert.DoesNotContain("only", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-05")]
    public async Task TabControlsAreRadioTabsWithPanelsAsync()
    {
        var tabs = new Tabs();
        const string Body = """
            <TabControl SelectedIndex="{Binding Tab}">
              <TabItem Header="Mail"><TextBlock Text="Inbox" /></TabItem>
              <TabItem Header="Files"><TextBlock Text="Documents" /></TabItem>
            </TabControl>
            """;

        var page = await fixture.CompileAsync(Body, tabs);
        var html = EngineFixture.Body(page, tabs);
        await fixture.PostAsync(Body, tabs, ("f0", "1"));

        Assert.Matches(
            "<div data-aioros-tabs class=\"c [^\"]+\"><div role=\"tablist\" data-change class=\"[^\"]+\">" +
            "<button type=\"button\" role=\"tab\" aria-selected=\"true\" tabindex=\"0\" class=\"(?<tab>[^\"]+)\"><input type=\"hidden\" name=\"f0\" value=\"0\">Mail</button>" +
            "<button type=\"button\" role=\"tab\" aria-selected=\"false\" tabindex=\"-1\" class=\"\\k<tab>\">Files</button></div>" +
            "<div role=\"tabpanel\" class=\"[^\"]*\"><div[^>]*>Inbox</div></div><div role=\"tabpanel\" hidden class=\"[^\"]*\"><div[^>]*>Documents</div></div></div>",
            html);
        Assert.Matches("\\.tb[0-9a-f]+\\[aria-selected=\"true\"\\]::after\\{display:block\\}", page.Css);
        Assert.Equal(1, tabs.Tab);
    }

    [Fact]
    [Trait("AC", "ITEMS-06")]
    public async Task TabStripsAreRadioTabsAsync()
    {
        var tabs = new Tabs { Tab = 1 };
        const string Body = """
            <TabStrip SelectedIndex="{Binding Tab}">
              <TabStripItem>Day</TabStripItem>
              <TabStripItem>Week</TabStripItem>
            </TabStrip>
            """;

        var page = await fixture.CompileAsync(Body, tabs);
        var html = EngineFixture.Body(page, tabs);
        await fixture.PostAsync(Body, tabs, ("f0", "0"));

        Assert.Matches($"<div role=\"radiogroup\" data-change class=\"c [^\"]+\"><label class=\"(?<tab>[^\"]+)\">{Radio} name=\"f0\" value=\"0\">Day</label><label class=\"\\k<tab>\">{Radio} name=\"f0\" value=\"1\" checked>Week</label></div>", html);
        Assert.Matches("\\.k[0-9a-f]+\\{position:relative;display:flex;flex-direction:row;align-items:center;cursor:pointer;white-space:nowrap;font-size:24px;", page.Css);
        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(0, tabs.Tab);
    }

    [Fact]
    [Trait("AC", "ITEMS-07")]
    public async Task TreeViewsAreNestedDetailsAsync()
    {
        var page = await fixture.CompileAsync("""
            <TreeView>
              <TreeViewItem Header="Documents" IsExpanded="True">
                <TreeViewItem Header="Report" IsSelected="True" />
                <TreeViewItem Header="Archive"><TreeViewItem Header="2025" /></TreeViewItem>
              </TreeViewItem>
            </TreeView>
            """);

        var html = EngineFixture.Body(page);

        Assert.Matches(
            "<ul class=\"tv[^\"]*\"><li><details open><summary class=\"(?<row>it[0-9a-f]+) (?<look>k[0-9a-f]+)\" style=\"padding-left:12px\"><svg class=\"chev\"[^>]*>.*?</svg>Documents</summary><ul class=\"tv\">" +
            "<li><div class=\"\\k<row> \\k<look> sel\" style=\"padding-left:28px\"><span class=\"tvs\"></span>Report</div></li>" +
            "<li><details><summary class=\"\\k<row> \\k<look>\" style=\"padding-left:28px\"><svg[^>]*>.*?</svg>Archive</summary><ul class=\"tv\"><li><div class=\"\\k<row> \\k<look>\" style=\"padding-left:44px\"><span class=\"tvs\"></span>2025</div></li></ul></details></li></ul></details></li></ul>",
            html);
        Assert.Contains("min-height:32px;}", page.Css, StringComparison.Ordinal);
        Assert.Matches("\\.it[0-9a-f]+\\.sel\\{background:rgba\\(0,120,215,0\\.6\\);", page.Css);
    }

    [Fact]
    [Trait("AC", "ITEMS-70")]
    public async Task BoundTreeViewsArePlannedToAnyDepthAsync()
    {
        const string Body = """
            <StackPanel>
              <TreeView ItemsSource="{Binding Folders}">
                <TreeView.ItemTemplate>
                  <TreeDataTemplate ItemsSource="{Binding Items}">
                    <TextBlock Text="{Binding Name}" />
                  </TreeDataTemplate>
                </TreeView.ItemTemplate>
              </TreeView>
              <TreeView ItemsSource="{Binding Regions}">
                <TreeView.DataTemplates>
                  <TreeDataTemplate DataType="m:Region" ItemsSource="{Binding Cities}"><TextBlock Text="{Binding Name}" /></TreeDataTemplate>
                  <TreeDataTemplate DataType="m:City" ItemsSource="{Binding Streets}"><TextBlock Text="{Binding Name}" FontWeight="Bold" /></TreeDataTemplate>
                  <TreeDataTemplate DataType="m:Street"><TextBlock Text="{Binding Name}" /></TreeDataTemplate>
                </TreeView.DataTemplates>
              </TreeView>
            </StackPanel>
            """;
        var compiled = new Forest(
            [new Folder("Docs", new Folder("Report")), new Folder("Music")],
            [new Region("North", new City("Oslo", new Street("Karl Johans gate")))]);
        var page = await fixture.CompileAsync(Body, compiled);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", page)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var deeper = new Forest(
            [new Folder("Docs"), new Folder("Music", new Folder("Rock", new Folder("1970s", new Folder("Live")))), new Folder("Photos", new Folder("2025"))],
            [new Region("South"), new Region("West", new City("Bergen"), new City("Stavanger", new Street("Kirkegata"), new Street("Torget")))]);
        var selected = await fixture.CompileAsync("""<TreeView ItemsSource="{Binding Folders}" SelectedItem="{Binding Picked}"><TreeView.ItemTemplate><TreeDataTemplate ItemsSource="{Binding Items}"><TextBlock Text="{Binding Name}" /></TreeDataTemplate></TreeView.ItemTemplate></TreeView>""", compiled);

        Assert.True(page.Reusable, page.Failure);
        foreach (var model in new[] { compiled, deeper, new Forest([], []) })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Body, model), model);
            Assert.Equal(expected, EngineFixture.Body(page, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        var html = EngineFixture.Body(page, deeper);
        Assert.Contains(">Live<", html, StringComparison.Ordinal);
        Assert.Contains("padding-left:60px", html, StringComparison.Ordinal);
        Assert.Contains(">Torget<", html, StringComparison.Ordinal);
        Assert.False(selected.Reusable);
        Assert.Contains("selected", selected.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-08")]
    public async Task CarouselsAreScrollSnapSlidesAsync()
    {
        var page = await fixture.CompileAsync("""
            <Carousel SelectedIndex="1" Height="120">
              <TextBlock Text="First" />
              <TextBlock Text="Second" />
              <TextBlock Text="Third" />
            </Carousel>
            """);

        var html = EngineFixture.Body(page);

        Assert.Matches(
            "<div data-aioros-carousel data-aioros-strip role=\"region\" aria-roledescription=\"carousel\" class=\"k[0-9a-f]+\"><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs\"><div[^>]*>First</div></div><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs sel\"><div[^>]*>Second</div></div><div role=\"group\" aria-roledescription=\"slide\" class=\"c cs\"><div[^>]*>Third</div></div></div>",
            html);
        Assert.Contains("display:flex;flex-direction:row;overflow-x:auto;scroll-snap-type:x mandatory;", page.Css, StringComparison.Ordinal);
        Assert.Contains(".cs{flex:0 0 100%;min-width:0;scroll-snap-align:start}.cs.sel{scroll-initial-target:nearest}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-09")]
    public async Task TableViewsAreTablesAsync()
    {
        var page = await fixture.CompileAsync(
            """
            <TableView ItemsSource="{Binding Products}">
              <TableView.Columns>
                <TableViewColumn Header="Name" Binding="{Binding Name}" x:DataType="m:Product" />
                <TableViewColumn Header="Stock" Binding="{Binding Stock}" Width="80" HorizontalContentAlignment="Right" x:DataType="m:Product" />
              </TableView.Columns>
            </TableView>
            """,
            new Inventory(new Product("only", 0)));

        var html = EngineFixture.Body(page, new Inventory(new Product("Pens", 12), new Product("Ink", 3), new Product("Paper", 500)));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches(
            "<table class=\"tt[0-9a-f]+\"><colgroup><col style=\"width:100%\"><col style=\"width:80px\"></colgroup>" +
            "<thead><tr><th>Name</th><th class=\"r\">Stock</th></tr></thead><tbody>" +
            "<tr class=\"(?<row>it[0-9a-f]+)\"><td>Pens</td><td class=\"r\">12</td></tr><tr class=\"\\k<row>\"><td>Ink</td><td class=\"r\">3</td></tr><tr class=\"\\k<row>\"><td>Paper</td><td class=\"r\">500</td></tr></tbody></table>",
            html);
    }

    [Fact]
    [Trait("AC", "ITEMS-105")]
    public async Task ATablesThemeGivesItsHeaderAndRowsTheirLookAsync()
    {
        const string Themes = """
            <StackPanel.Resources>
              <ControlTheme x:Key="Scroll" TargetType="ScrollViewer">
                <Setter Property="Template">
                  <ControlTemplate>
                    <DockPanel>
                      <Border Name="PART_Header" DockPanel.Dock="Top" BorderBrush="#e5e5e5" BorderThickness="0,0,0,1"><TableViewColumnHeadersPresenter /></Border>
                      <ScrollContentPresenter Name="PART_ContentPresenter" />
                    </DockPanel>
                  </ControlTemplate>
                </Setter>
              </ControlTheme>
              <ControlTheme x:Key="Table" TargetType="TableView">
                <Setter Property="Template">
                  <ControlTemplate>
                    <ScrollViewer Name="PART_ScrollViewer" Theme="{StaticResource Scroll}"><ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" /></ScrollViewer>
                  </ControlTemplate>
                </Setter>
              </ControlTheme>
              <ControlTheme x:Key="{x:Type TableViewColumnHeader}" TargetType="TableViewColumnHeader">
                <Setter Property="Height" Value="40" />
                <Setter Property="Template">
                  <ControlTemplate><ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" Content="{TemplateBinding Content}" /></ControlTemplate>
                </Setter>
              </ControlTheme>
              <ControlTheme x:Key="{x:Type TableViewRow}" TargetType="TableViewRow">
                <Setter Property="BorderBrush" Value="#e5e5e5" />
                <Setter Property="BorderThickness" Value="0,0,0,1" />
                <Setter Property="Template">
                  <ControlTemplate>
                    <Border Name="Border" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><TableViewCellsPresenter Name="PART_CellsPresenter" /></Border>
                  </ControlTemplate>
                </Setter>
                <Style Selector="^:nth-last-child(1)"><Setter Property="BorderThickness" Value="0" /></Style>
              </ControlTheme>
            </StackPanel.Resources>
            """;
        var written = await fixture.CompileAsync($$"""
            <StackPanel>
              {{Themes}}
              <TableView Theme="{StaticResource Table}" ItemsSource="{Binding Products}">
                <TableView.Columns><TableViewColumn Header="Name" Binding="{Binding Name}" x:DataType="m:Product" /></TableView.Columns>
              </TableView>
            </StackPanel>
            """, new Inventory(new Product("a", 1), new Product("b", 2), new Product("c", 3)));
        var bound = await fixture.PlansLikeAvaloniaAsync($$"""
            <StackPanel>
              {{Themes}}
              <TableView Theme="{StaticResource Table}" ItemsSource="{Binding Products}">
                <TableView.Columns><TableViewColumn Header="Name" Binding="{Binding Name}" x:DataType="m:Product" /></TableView.Columns>
              </TableView>
            </StackPanel>
            """, new Inventory(new Product("only", 0)), new Inventory(new Product("a", 1), new Product("b", 2), new Product("c", 3)));
        var fluent = await fixture.CompileAsync("""
            <TableView ItemsSource="{Binding Products}">
              <TableView.Columns><TableViewColumn Header="Name" Binding="{Binding Name}" x:DataType="m:Product" /></TableView.Columns>
            </TableView>
            """, new Inventory(new Product("a", 1), new Product("b", 2)));

        foreach (var page in (CompiledPage[])[written, bound])
        {
            var table = Regex.Match(page.Css, "(?m)^\\.(tt[0-9a-f]+)\\{.*$").Value;
            Assert.Matches(" th\\{[^}]*height:40px;[^}]*background:transparent", table);
            Assert.Matches(" thead tr\\{border-bottom:1px solid #e5e5e5\\}", table);
            Assert.Matches(" tbody tr\\{border-bottom:1px solid #e5e5e5\\}", table);
            Assert.Matches(" tbody tr:last-child\\{border-bottom:0px solid transparent\\}", table);
        }

        var plain = Regex.Match(fluent.Css, "(?m)^\\.(tt[0-9a-f]+)\\{.*$").Value;
        Assert.DoesNotContain("tbody tr{", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("background:transparent", Regex.Match(plain, " th\\{[^}]*\\}").Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-91")]
    public async Task DataGridsAreTablesAsync()
    {
        const string Body = """
            <DataGrid ItemsSource="{Binding Products}" IsReadOnly="True">
              <DataGrid.Columns>
                <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" x:DataType="m:Product" />
                <DataGridTextColumn Header="Hidden" Binding="{Binding Name}" IsVisible="False" x:DataType="m:Product" />
                <DataGridTemplateColumn Header="Stock" Width="80">
                  <DataGridTemplateColumn.CellTemplate>
                    <DataTemplate x:DataType="m:Product"><TextBlock Text="{Binding Stock}" /></DataTemplate>
                  </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
              </DataGrid.Columns>
            </DataGrid>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Inventory(new Product("only", 0)), new Inventory(new Product("Pens", 12), new Product("Ink", 3), new Product("Paper", 500)), new Inventory());
        var empty = await fixture.CompileAsync(Body, new Inventory());

        var html = EngineFixture.Body(page, new Inventory(new Product("Pens", 12), new Product("Ink", 3), new Product("Paper", 500)));

        Assert.Matches(
            "<table class=\"dg[0-9a-f]+\"><colgroup><col style=\"width:100%\"><col style=\"width:80px\"></colgroup>" +
            "<thead><tr><th>Name</th><th>Stock</th></tr></thead><tbody>",
            html);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(html, "<tr class="));
        Assert.Matches("<tr class=\"[^\"]*\"><td>Paper</td><td><div[^>]*>500</div></td></tr></tbody></table>", html);
        Assert.False(empty.Reusable);
        Assert.Contains("DataGrid", empty.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-92")]
    public async Task TemplateDataGridsRepeatTheirControlsListAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/GaugeTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <m:Accounts Rows="{Binding Products}" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Inventory(new Product("only", 0)), new Inventory(new Product("Pens", 12), new Product("Ink", 3)), new Inventory());

        Assert.Contains("<td>Ink</td>", EngineFixture.Body(page, new Inventory(new Product("Pens", 12), new Product("Ink", 3))), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-10")]
    public async Task PipsPagersAreRadioPipsWithStepsAsync()
    {
        var paging = new Paging();
        const string Body = """<PipsPager NumberOfPages="{Binding Count}" SelectedPageIndex="{Binding Page}" />""";

        var page = await fixture.CompileAsync(Body, paging);
        var html = EngineFixture.Body(page, paging);
        var posted = await fixture.PostAsync(Body, new Paging(), ("f0", "2"));
        var next = await fixture.PostAsync(Body, new Paging(), ("f0", "2"), ("f2", "on"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches(
            $"<div role=\"radiogroup\" class=\"c\" style=\"flex-direction:row\"><label class=\"(?<pip>pp[0-9a-f]+)\">{Radio} name=\"f0\" value=\"0\" checked></label>" +
            $"<label class=\"\\k<pip>\">{Radio} name=\"f0\" value=\"1\"></label><label class=\"\\k<pip>\">{Radio} name=\"f0\" value=\"2\"></label><label class=\"\\k<pip>\">{Radio} name=\"f0\" value=\"3\"></label></div>" +
            "<button type=\"submit\" name=\"f1\" value=\"on\" aria-label=\"Previous page\" class=\"[^\"]+\" style=\"visibility:hidden\"><svg[^>]*><path[^>]*/></svg></button>" +
            "<button type=\"submit\" name=\"f2\" value=\"on\" aria-label=\"Next page\" class=\"[^\"]+\"><svg[^>]*><path[^>]*/></svg></button>",
            html);
        Assert.Contains("value=\"2\" checked>", EngineFixture.Body(posted.Page!, paging), StringComparison.Ordinal);
        Assert.Contains("value=\"3\" checked>", EngineFixture.Body(next.Page!, paging), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-11")]
    public async Task ItemsBaseTypesGoThroughTheGenericPathAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <SelectingItemsControl><TextBlock Text="One" /></SelectingItemsControl>
              <HeaderedItemsControl Header="Two"><TextBlock Text="Three" /></HeaderedItemsControl>
              <HeaderedSelectingItemsControl Header="Four"><TextBlock Text="Five" /></HeaderedSelectingItemsControl>
            </StackPanel>
            """);

        Assert.Matches("^<div class=\"c k[0-9a-f]+\"><div class=\"c\"></div><div class=\"c\"></div><div class=\"c\"></div></div>$", html);
    }

    [Fact]
    [Trait("AC", "ENGINE-19")]
    public async Task EmptyListsLearnTheirTemplateFromTheDataTypeAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border.poster"><Setter Property="Background" Value="#123456" /><Setter Property="Padding" Value="6" /></Style>
                <Style Selector="Border.poster TextBlock.title"><Setter Property="Foreground" Value="#ABCDEF" /></Style>
              </StackPanel.Styles>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:Poster">
                    <Border Classes="poster">
                      <Grid ColumnDefinitions="*,Auto">
                        <TextBlock Classes="title" Text="{Binding Title}" />
                        <TextBlock Grid.Column="1" Text="{Binding Url}" />
                      </Grid>
                    </Border>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <ListBox ItemsSource="{Binding Items}">
                <ListBox.ItemTemplate>
                  <DataTemplate x:DataType="m:Poster"><TextBlock Text="{Binding Title}" /></DataTemplate>
                </ListBox.ItemTemplate>
              </ListBox>
            </StackPanel>
            """;
        var full = new Posters(new Poster("/a.png", "Alpha"), new Poster("/b.png", "Beta"));
        var empty = await fixture.CompileAsync(Xaml, new Posters());
        var learned = await fixture.CompileAsync(Xaml, full);
        var untyped = await fixture.CompileAsync(Xaml.Replace(" x:DataType=\"m:Poster\"", string.Empty, StringComparison.Ordinal).Replace("{Binding Title}", "{ReflectionBinding Title}", StringComparison.Ordinal).Replace("{Binding Url}", "{ReflectionBinding Url}", StringComparison.Ordinal), new Posters());

        Assert.True(empty.Reusable, empty.Failure);
        Assert.Equal(EngineFixture.Body(learned, full), EngineFixture.Body(empty, full));
        Assert.Empty(learned.Css.Split('\n').Except(empty.Css.Split('\n')));
        Assert.DoesNotContain("Alpha", EngineFixture.Body(empty, new Posters()), StringComparison.Ordinal);
        Assert.True(untyped.Reusable, untyped.Failure);
        Assert.Contains(">Alpha<", EngineFixture.Body(untyped, full), StringComparison.Ordinal);
        var unknown = await fixture.CompileAsync("""<ListBox ItemsSource="{Binding Loose}" />""", new LooseItems());
        Assert.False(unknown.Reusable);
        Assert.Contains("empty list", unknown.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-20")]
    public async Task StringFormatsArePlannedAsync()
    {
        var page = await fixture.CompileAsync("""<TextBlock Text="{Binding Count, StringFormat='{}{0:N0} sold'}" />""", new Tally(3));
        var saved = Aioros.Runtime.Rendering.PlansFile.Read(
            Aioros.Runtime.Rendering.PlansFile.Write([new Aioros.Runtime.Rendering.PlannedPage("/", "light", page)]).Json,
            new Aioros.Avalonia.Plans.AdapterSlotResolver()).Single().Page;
        var expected = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:N0} sold", 12345);

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains($">{expected}<", EngineFixture.Body(page, new Tally(12345)), StringComparison.Ordinal);
        Assert.Contains($">{expected}<", EngineFixture.Body(saved, new Tally(12345)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-21")]
    public async Task TemplatesBoundToTheItemArePlannedAsync()
    {
        var page = await fixture.CompileAsync("""
            <ItemsControl ItemsSource="{Binding Shots}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="x:String"><TextBlock Text="{Binding}" /></DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """, new Tally(0, "one"));
        var html = EngineFixture.Body(page, new Tally(0, "alpha", "beta"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(">alpha<", html, StringComparison.Ordinal);
        Assert.Contains(">beta<", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-12")]
    public async Task ItemsWrittenInXamlArePlannedAsync()
    {
        var page = await fixture.CompileAsync("""
            <ItemsControl>
              <TextBlock Text="First" />
              <TextBlock Text="Second" />
            </ItemsControl>
            """, new Article("ignored"));
        var html = EngineFixture.Body(page, new Article("other"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("role=\"listitem\"[^>]*><[^>]*>First<", html);
        Assert.Contains(">Second<", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-13")]
    public async Task ComboBoxesAreAsTallAsInAvaloniaAsync()
    {
        const string Body = """
            <StackPanel>
              <ComboBox SelectedIndex="0"><x:String>Free</x:String></ComboBox>
              <TextBlock Text="After" />
              <ComboBox MinHeight="48" SelectedIndex="0"><x:String>Pro</x:String></ComboBox>
              <TextBlock Text="End" />
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;

        Assert.Matches("\\{[^}]*padding:5px 31px 7px 12px;[^}]*height:32px;\\}", page.Css);
        Assert.Matches("\\{[^}]*padding:5px 31px 7px 12px;[^}]*height:48px;\\}", page.Css);
        Assert.DoesNotMatch("min-height:(?!0;)", page.Css);
        Assert.Equal([32.0, 32 + 17 + 48.0], laid.Where(text => text.Text is "After" or "End").Select(text => text.Y));
    }

    [Fact]
    [Trait("AC", "ITEMS-14")]
    public async Task SelectedItemsHaveTheirPressedLookAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ListBox SelectionMode="Multiple"><ListBoxItem IsSelected="True">Read</ListBoxItem></ListBox>
              <TreeView><TreeViewItem Header="Report.pdf" IsSelected="True" /></TreeView>
            </StackPanel>
            """);

        foreach (var marker in new[] { ":has\\(>input:checked\\)", "\\.sel" })
        {
            var hover = Regex.Match(page.Css, $"\\.(it[0-9a-f]+){marker}:hover\\{{([^}}]*)\\}}");
            var pressed = Regex.Match(page.Css, $"\\.{hover.Groups[1].Value}{marker}:active\\{{([^}}]*)\\}}");
            Assert.True(pressed.Success, marker);
            Assert.NotEqual(hover.Groups[2].Value, pressed.Groups[1].Value);
        }
    }

    [Fact]
    [Trait("AC", "ITEMS-15")]
    public async Task BoundTabHeadersTakeTheLookOfTheirOwnSelectionAsync()
    {
        var page = await fixture.CompileAsync("""
            <TabControl ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}">
              <TabControl.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></TabControl.ItemTemplate>
              <TabControl.ContentTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name, StringFormat='Shown: {0}'}" /></DataTemplate></TabControl.ContentTemplate>
            </TabControl>
            """, new Shelf(0, new Item("Mail"), new Item("Files")));

        var html = EngineFixture.Body(page, new Shelf(1, new Item("a1"), new Item("b2"), new Item("c3")));
        var headers = Regex.Matches(html, "<button type=\"button\" role=\"tab\" aria-selected=\"(?<checked>true|false)\" tabindex=\"-?\\d\" class=\"(?<tab>[^\"]+)\">(?:<input type=\"hidden\" name=\"f0\" value=\"\\d\">)?<div class=\"(?<header>[^\"]+)\">(?<name>\\w+)</div></button>");
        var tab = headers[0].Groups["tab"].Value.Split(' ').First(name => name.StartsWith("it", StringComparison.Ordinal));
        var unselected = Regex.Match(page.Css, $"\\.{tab}\\{{[^}}]*color:([^;}}]+)").Groups[1].Value;
        var selected = Regex.Match(page.Css, $"\\.{tab}\\[aria-selected=\"true\"\\]\\{{[^}}]*color:([^;}}]+)").Groups[1].Value;

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(["a1", "b2", "c3"], headers.Select(header => header.Groups["name"].Value));
        Assert.Equal(["false", "true", "false"], headers.Select(header => header.Groups["checked"].Value));
        Assert.Equal(2, Regex.Count(html, "<div role=\"tabpanel\" hidden"));
        Assert.Single(headers.Select(header => header.Groups["header"].Value).Distinct());
        Assert.DoesNotMatch($"\\.{Regex.Escape(headers[0].Groups["header"].Value.Split(' ')[^1])}\\{{[^}}]*color:", page.Css);
        Assert.NotEqual(unselected, selected);
    }

    [Fact]
    [Trait("AC", "ITEMS-17")]
    public async Task BoundTabPanelsTakeTheTemplateOfTheirItemTypeAsync()
    {
        const string Body = """
            <TabControl ItemsSource="{Binding Items}">
              <TabControl.DataTemplates>
                <DataTemplate x:DataType="m:Circle"><TextBlock Text="{Binding Radius, StringFormat='circle {0}'}" /></DataTemplate>
                <DataTemplate x:DataType="m:Square"><UserControl><TextBlock Text="{Binding Label}" /></UserControl></DataTemplate>
              </TabControl.DataTemplates>
              <TabControl.ItemTemplate><DataTemplate><TextBlock Text="tab" /></DataTemplate></TabControl.ItemTemplate>
              <TabControl.ContentTemplate><DataTemplate><ContentControl Content="{Binding}" /></DataTemplate></TabControl.ContentTemplate>
            </TabControl>
            """;
        var page = await fixture.CompileAsync(Body, new Shapes(new Circle(1), new Square(2, "two")));
        var other = new Shapes(new Circle(3), new Square(4, "four"), new Circle(5));

        var plain = await fixture.CompileAsync("""<TabControl ItemsSource="{Binding Items}" />""", new Catalog(new Item("a"), new Item("b")));
        var catalog = new Catalog(new Item("c"), new Item("d"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Body, other), other), EngineFixture.Body(page, other));
        Assert.Matches(">circle 3<.*>four<.*>circle 5<", EngineFixture.Body(page, other));
        Assert.True(plain.Reusable, plain.Failure);
        Assert.Equal(EngineFixture.Body(await fixture.CompileAsync("""<TabControl ItemsSource="{Binding Items}" />""", catalog), catalog), EngineFixture.Body(plain, catalog));
    }

    [Fact]
    [Trait("AC", "ITEMS-18")]
    public async Task TwoWaySelectionsAreMarkedToPostWhenTheyChangeAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <ListBox ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}" />
              <TabControl ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}" />
              <TabStrip ItemsSource="{Binding Items}" SelectedItem="{Binding Selected, Mode=OneWay}" />
              <ComboBox ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}" />
              <ListBox><ListBoxItem>Fixed</ListBoxItem></ListBox>
            </StackPanel>
            """, new Shelf(0, new Item("a"), new Item("b")));

        var html = EngineFixture.Body(page, new Shelf(1, new Item("c"), new Item("d")));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(
            ["<div role=\"radiogroup\" data-change", "<div role=\"tablist\" data-change", "<div role=\"radiogroup\" class", "<select name=\"f3\" data-change", "<div role=\"radiogroup\" class"],
            Regex.Matches(html, "<div role=\"(radiogroup|tablist)\" (data-change|class)|<select name=\"f\\d\" (data-change|class)").Select(match => match.Value));
    }

    [Fact]
    [Trait("AC", "ITEMS-16")]
    public async Task ComboBoxOptionsAreKeyedByTheirIndexAsync()
    {
        const string Body = """<ComboBox ItemsSource="{Binding Categories}" SelectedItem="{Binding Category}" />""";
        var work = new Category("Work");
        var page = await fixture.CompileAsync(Body, new Assignment(0, new Category("Home"), new Category("Errands")));
        var html = EngineFixture.Body(page, new Assignment(1, new Category("Home"), work, new Category("Errands")));
        var engine = new Assignment(0, new Category("Home"), work, new Category("Errands"));
        await fixture.PostAsync(Body, engine, ("f0", "1"));
        var planned = new Assignment(0, new Category("Home"), work, new Category("Errands"));
        var status = await PlanForms.ApplyAsync(page.Plan, [planned], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", "2"), new Plans.AdapterSlotResolver());
        const string Text = "Aioros.Avalonia.Tests.Models.Category";

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches($"<option value=\"0\">{Text}</option><option value=\"1\" selected>{Text}</option><option value=\"2\">{Text}</option></select>", html);
        Assert.Same(work, engine.Category);
        Assert.Equal(PostStatus.Rendered, status);
        Assert.Same(planned.Categories[2], planned.Category);
    }

    [Fact]
    [Trait("AC", "ITEMS-28")]
    public async Task ContainerThemesStyleTabsBeforeTheirTemplatesAsync()
    {
        var html = await fixture.HtmlAsync("""
            <UserControl.Resources>
              <ControlTheme x:Key="Line" TargetType="TabItem" BasedOn="{StaticResource {x:Type TabItem}}">
                <Setter Property="Padding" Value="3" />
              </ControlTheme>
              <ControlTheme x:Key="Lined" TargetType="TabControl" BasedOn="{StaticResource {x:Type TabControl}}">
                <Setter Property="ItemContainerTheme" Value="{StaticResource Line}" />
              </ControlTheme>
            </UserControl.Resources>
            <TabControl Theme="{StaticResource Lined}">
              <TabItem Header="Mail"><StackPanel><TextBlock Text="Inbox" /></StackPanel></TabItem>
              <TabItem Header="Files">
                <TabControl Theme="{StaticResource Lined}">
                  <TabItem Header="Recent"><StackPanel><TextBlock Text="Documents" /></StackPanel></TabItem>
                </TabControl>
              </TabItem>
            </TabControl>
            """);

        Assert.Single(Regex.Matches(html, ">Inbox<"));
        Assert.Single(Regex.Matches(html, ">Documents<"));
        Assert.Matches("<input type=\"hidden\" name=\"f0\" value=\"0\">Mail</button>", html);
    }

    [Fact]
    [Trait("AC", "ITEMS-49")]
    public async Task StaticListsOfObjectsAreComboBoxOptionsAsync()
    {
        const string Body = """<ComboBox ItemsSource="{x:Static m:SortOrder.All}" SelectedItem="{Binding Order}" />""";
        var page = await fixture.CompileAsync(Body, new OrderPick(0));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", page)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var expected = EngineFixture.Body(await fixture.CompileAsync(Body, new OrderPick(1)), new OrderPick(1));
        var engine = new OrderPick(0);
        await fixture.PostAsync(Body, engine, ("f0", "2"));
        var planned = new OrderPick(0);
        var status = await PlanForms.ApplyAsync(saved.Plan, [planned], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", "2"), new Plans.AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(expected, EngineFixture.Body(page, new OrderPick(1)));
        Assert.Equal(expected, EngineFixture.Body(saved, new OrderPick(1)));
        Assert.Contains("<option value=\"1\" selected>Title</option>", expected, StringComparison.Ordinal);
        Assert.Same(SortOrder.All[2], engine.Order);
        Assert.Equal(PostStatus.Rendered, status);
        Assert.Same(SortOrder.All[2], planned.Order);
    }
    [Fact]
    [Trait("AC", "ITEMS-98")]
    public async Task BoundSelectedItemsArePlannedAsync()
    {
        string[] styles = ["Bold", "Italic", "Underline"];
        const string Body = """<ListBox SelectionMode="Multiple" ItemsSource="{Binding All}" SelectedItems="{Binding Chosen}" />""";
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Picks(styles, "Italic"), new Picks(styles, "Bold", "Underline"));

        var html = EngineFixture.Body(page, new Picks(styles, "Bold", "Underline"));
        Assert.Matches("<input type=\"checkbox\"[^>]* checked[^>]*>Bold</label>", html);
        Assert.Matches("<input type=\"checkbox\"(?![^>]* checked)[^>]*>Italic</label>", html);
        Assert.Matches("<input type=\"checkbox\"[^>]* checked[^>]*>Underline</label>", html);
    }
    [Fact]
    [Trait("AC", "ITEMS-99")]
    public async Task ListItemsTakeTheirThemesBoxAndNameAsync()
    {
        var page = await fixture.CompileAsync("""
            <ListBox SelectionMode="Multiple">
              <ListBox.ItemContainerTheme>
                <ControlTheme TargetType="ListBoxItem">
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="CornerRadius" Value="6" />
                  <Setter Property="Height" Value="36" />
                  <Setter Property="FontWeight" Value="Medium" />
                  <Setter Property="HorizontalContentAlignment" Value="Center" />
                  <Setter Property="VerticalContentAlignment" Value="Center" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                                        BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Content="{TemplateBinding Content}" />
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:selected"><Setter Property="BorderBrush" Value="#171717" /></Style>
                </ControlTheme>
              </ListBox.ItemContainerTheme>
              <ListBoxItem AutomationProperties.Name="Bold"><PathIcon Data="M0,0 L10,10" /></ListBoxItem>
              <ListBoxItem Content="Italic" />
            </ListBox>
            """);

        var html = EngineFixture.Body(page);
        var item = Regex.Match(html, "<label class=\"([^\"]+)\"><input type=\"checkbox\"[^>]*><span class=\"vh\">Bold</span>").Groups[1].Value.Split(' ');
        var rules = string.Join('\n', page.Css.Split('\n').Where(rule => item.Any(name => rule.StartsWith($".{name}", StringComparison.Ordinal))));
        Assert.Matches("height:36px;font-weight:500;justify-content:center;align-items:center;border-width:1px 1px 1px 1px;border-style:solid;border-radius:6px 6px 6px 6px;", rules);
        Assert.Matches(":has\\(>input:checked\\)\\{[^}]*border-color:#171717;", rules);
    }

    [Fact]
    [Trait("AC", "ITEMS-100")]
    public async Task ListItemsTakeTheLookOfTheirPositionAsync()
    {
        const string Styles = """
            <StackPanel.Styles>
              <Style Selector="ListBox.group > ListBoxItem:nth-child(1)"><Setter Property="CornerRadius" Value="8,0,0,8" /></Style>
              <Style Selector="ListBox.group > ListBoxItem:nth-last-child(1)"><Setter Property="CornerRadius" Value="0,8,8,0" /></Style>
              <Style Selector="ListBox.group > ListBoxItem:nth-child(1):nth-last-child(1)"><Setter Property="CornerRadius" Value="8" /></Style>
            </StackPanel.Styles>
            """;
        var bound = $$"""
            <StackPanel>
              {{Styles}}
              <ListBox Classes="group" ItemsSource="{Binding Items}">
                <ListBox.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></ListBox.ItemsPanel>
                <ListBox.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></ListBox.ItemTemplate>
              </ListBox>
            </StackPanel>
            """;
        Catalog Of(int count) => new([.. Enumerable.Range(1, count).Select(index => new Item("i" + index))]);

        var planned = await fixture.PlansLikeAvaloniaAsync(bound, Of(3), Of(1), Of(2), Of(3), Of(5));
        var written = await fixture.CompileAsync($$"""
            <StackPanel>
              {{Styles}}
              <ListBox Classes="group"><ListBoxItem Content="a" /><ListBoxItem Content="b" /><ListBoxItem Content="c" /></ListBox>
            </StackPanel>
            """);

        var chosen = await fixture.PlansLikeAvaloniaAsync($$"""
            <StackPanel>
              {{Styles}}
              <ListBox Classes="group" SelectionMode="Toggle" ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}">
                <ListBox.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></ListBox.ItemTemplate>
              </ListBox>
            </StackPanel>
            """, new Shelf(1, new Item("a"), new Item("b"), new Item("c")), new Shelf(2, new Item("a"), new Item("b"), new Item("c")));
        Assert.Equal(["8px 0px 0px 8px", "none", "none", "none", "0px 8px 8px 0px"], Corners(planned, EngineFixture.Body(planned, Of(5))));
        Assert.Equal(["8px 8px 8px 8px"], Corners(planned, EngineFixture.Body(planned, Of(1))));
        Assert.Equal(["8px 0px 0px 8px", "none", "0px 8px 8px 0px"], Corners(written, EngineFixture.Body(written)));
        Assert.Contains("value=\"1\" checked>", EngineFixture.Body(chosen, new Shelf(1, new Item("a"), new Item("b"), new Item("c"))), StringComparison.Ordinal);
    }

    private static List<string> Corners(CompiledPage page, string html) =>
        [.. Regex.Matches(html, "<label class=\"([^\"]+)\">").Select(label => label.Groups[1].Value.Split(' ')
            .Select(name => Regex.Match(page.Css, $"(?m)^\\.{name}\\{{[^}}]*border-radius:([^;]+);")).FirstOrDefault(rule => rule.Success)?.Groups[1].Value ?? "none")];

    [Fact]
    [Trait("AC", "ITEMS-101")]
    public async Task TabsSitInTheirThemesStripAsync()
    {
        var page = await fixture.CompileAsync("""
            <TabControl>
              <TabControl.Theme>
                <ControlTheme TargetType="TabControl">
                  <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></Setter>
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel Spacing="8">
                        <Border Name="PART_TabsList" Background="#f5f5f5" CornerRadius="10" Padding="3" Height="36" HorizontalAlignment="Left">
                          <ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" />
                        </Border>
                        <ContentPresenter Name="PART_SelectedContentHost" Content="{TemplateBinding SelectedContent}" ContentTemplate="{TemplateBinding SelectedContentTemplate}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </TabControl.Theme>
              <TabControl.ItemContainerTheme>
                <ControlTheme TargetType="TabItem">
                  <Setter Property="Background" Value="Transparent" />
                  <Setter Property="BorderBrush" Value="Transparent" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="CornerRadius" Value="6" />
                  <Setter Property="Padding" Value="8,4" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_LayoutRoot" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                              BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Header}" ContentTemplate="{TemplateBinding HeaderTemplate}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:selected">
                    <Setter Property="Background" Value="#ffffff" />
                    <Setter Property="BorderBrush" Value="#e5e5e5" />
                  </Style>
                  <Style Selector="^:selected /template/ Border#PART_LayoutRoot"><Setter Property="BoxShadow" Value="0 1 2 0 #0d000000" /></Style>
                  <Style Selector="^:disabled"><Setter Property="Opacity" Value="0.5" /></Style>
                </ControlTheme>
              </TabControl.ItemContainerTheme>
              <TabItem Header="Account"><TextBlock Text="Your account" /></TabItem>
              <TabItem Header="Password"><TextBlock Text="Your password" /></TabItem>
              <TabItem Header="Billing" IsEnabled="False"><TextBlock Text="Your bills" /></TabItem>
            </TabControl>
            """);

        var html = EngineFixture.Body(page);
        var strip = Regex.Match(html, "<div role=\"tablist\" class=\"([^\"]+)\">(<button [^>]*>(?:(?!</button>).)*</button>){3}</div>");
        Assert.True(strip.Success, html);
        Assert.Matches("background:#f5f5f5;[^}]*border-radius:10px[^}]*padding:3px", Rule(page, strip.Groups[1].Value.Split(' ')[^1]));
        Assert.Matches("height:36px", Rule(page, strip.Groups[1].Value.Split(' ')[^1]));
        Assert.Matches("</div><div role=\"tabpanel\" class=\"[^\"]*\"><div[^>]*>Your account</div></div><div role=\"tabpanel\" hidden class=\"[^\"]*\"><div[^>]*>Your password</div></div><div role=\"tabpanel\" hidden class=\"[^\"]*\">(<div[^>]*>Your bills</div>)?</div>", html);
        var labels = Regex.Matches(html, "<button type=\"button\" role=\"tab\" (aria-selected=\"[^\"]+\" tabindex=\"-?\\d\"(?: disabled)?) class=\"([^\"]+)\">").Select(match => (Attributes: match.Groups[1].Value, Classes: match.Groups[2].Value)).ToList();
        Assert.Matches("border-width:1px 1px 1px 1px;border-style:solid;", string.Join('\n', labels[0].Classes.Split(' ').Select(name => Rule(page, name))));
        Assert.Matches("\\[aria-selected=\"true\"\\]\\{[^}]*background:#ffffff;[^}]*border-color:#e5e5e5;[^}]*box-shadow:", page.Css);
        Assert.EndsWith(" disabled", labels[2].Attributes, StringComparison.Ordinal);
        Assert.DoesNotContain(" disabled", labels[1].Attributes, StringComparison.Ordinal);
        Assert.Matches("opacity:0\\.5", string.Join('\n', labels[2].Classes.Split(' ').Select(name => Rule(page, name))));
    }

    [Fact]
    [Trait("AC", "ITEMS-102")]
    public async Task BoundTabsSitInTheirThemesStripAsync()
    {
        const string Body = """
            <TabControl ItemsSource="{Binding Items}" SelectedIndex="{Binding Index}">
              <TabControl.Theme>
                <ControlTheme TargetType="TabControl">
                  <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></Setter>
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel Spacing="8">
                        <Border Name="PART_TabsList" Background="#f5f5f5" CornerRadius="10" Padding="3" Height="36" HorizontalAlignment="Left">
                          <ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" />
                        </Border>
                        <ContentPresenter Name="PART_SelectedContentHost" Content="{TemplateBinding SelectedContent}" ContentTemplate="{TemplateBinding SelectedContentTemplate}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </TabControl.Theme>
              <TabControl.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></TabControl.ItemTemplate>
              <TabControl.ContentTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name, StringFormat='About {0}'}" /></DataTemplate></TabControl.ContentTemplate>
            </TabControl>
            """;
        Leaves Of(int count, int index) => new([.. Enumerable.Range(1, count).Select(at => new Item("t" + at))], index);

        var page = await fixture.PlansLikeAvaloniaAsync(Body, Of(2, 0), Of(1, 0), Of(2, 1), Of(4, 2));

        var html = EngineFixture.Body(page, Of(4, 2));
        var strip = Regex.Match(html, "<div role=\"tablist\" data-change class=\"([^\"]+)\">(<button [^>]*>(?:(?!</button>).)*</button>){4}</div>");
        Assert.True(strip.Success, html);
        Assert.Matches("background:#f5f5f5;[^}]*border-radius:10px", Rule(page, strip.Groups[1].Value.Split(' ')[^1]));
        Assert.Equal(["false", "false", "true", "false"], Regex.Matches(html, "role=\"tab\" aria-selected=\"(\\w+)\"").Select(match => match.Groups[1].Value));
        Assert.Matches("<div role=\"tabpanel\" class=\"[^\"]*\"><div[^>]*>About t3</div></div>", html);
        Assert.Equal(3, Regex.Count(html, "<div role=\"tabpanel\" hidden"));
    }

    private static string Rule(CompiledPage page, string name) =>
        string.Join('\n', page.Css.Split('\n').Where(rule => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal)));

    [Fact]
    [Trait("AC", "ITEMS-121")]
    public async Task AnEmptyPanelWhoseHostHidesIsOutOfTheLayoutAsync()
    {
        static string Tabs(string host) => $$"""
            <TabControl SelectedIndex="0">
              <TabControl.Theme>
                <ControlTheme TargetType="TabControl">
                  <Setter Property="ItemsPanel"><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></Setter>
                  <Setter Property="Template">
                    <ControlTemplate>
                      <StackPanel Spacing="8">
                        <Border Name="PART_TabsList" Background="#f5f5f5" Padding="3" Height="36" HorizontalAlignment="Left">
                          <ItemsPresenter Name="PART_ItemsPresenter" ItemsPanel="{TemplateBinding ItemsPanel}" />
                        </Border>
                        <ContentPresenter Name="PART_SelectedContentHost"{{host}} Content="{TemplateBinding SelectedContent}" ContentTemplate="{TemplateBinding SelectedContentTemplate}" />
                      </StackPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </TabControl.Theme>
              <TabItem Header="Overview" />
              <TabItem Header="Analytics"><TextBlock Text="Charts" /></TabItem>
            </TabControl>
            """;

        var hiding = await fixture.CompileAsync(Tabs(" IsVisible=\"{TemplateBinding SelectedContent, Converter={x:Static ObjectConverters.IsNotNull}}\""));
        var showing = await fixture.CompileAsync(Tabs(string.Empty));
        var second = await fixture.CompileAsync(Tabs(" IsVisible=\"{TemplateBinding SelectedContent, Converter={x:Static ObjectConverters.IsNotNull}}\"").Replace("SelectedIndex=\"0\"", "SelectedIndex=\"1\"", StringComparison.Ordinal));
        string Panel(CompiledPage page) => Regex.Match(EngineFixture.Body(page), "<div role=\"tabpanel\" class=\"(?<class>[^\"]+)\">").Groups["class"].Value.Split(' ')[^1];

        Assert.Contains($".{Panel(hiding)}:empty{{display:none}}", hiding.Css, StringComparison.Ordinal);
        Assert.Matches("<div role=\"tabpanel\" hidden class=\"[^\"]+\"><div[^>]*>Charts</div></div>", EngineFixture.Body(hiding));
        Assert.DoesNotContain(":empty{display:none}", showing.Css, StringComparison.Ordinal);
        Assert.Contains(":empty{display:none}", second.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-333")]
    public async Task EmptyListsLearnTheirTemplateFromAnAbstractDataTypeAsync()
    {
        static string Xaml(string type) => $$"""
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:{{type}}"><TextBlock Text="{Binding Title}" /></DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """;
        var rows = new InterfaceRows(new InterfaceRow("Alpha"), new InterfaceRow("Beta"));
        var cards = new BaseCards(new ConcreteCard("Alpha"), new ConcreteCard("Beta"));

        var emptyRows = await fixture.CompileAsync(Xaml(nameof(IInterfaceRow)), new InterfaceRows());
        var learnedRows = await fixture.CompileAsync(Xaml(nameof(IInterfaceRow)), rows);
        var emptyCards = await fixture.CompileAsync(Xaml(nameof(BaseCard)), new BaseCards());
        var learnedCards = await fixture.CompileAsync(Xaml(nameof(BaseCard)), cards);
        var unmade = await fixture.CompileAsync(Xaml(nameof(UnmadeCard)), new UnmadeCards());

        Assert.True(emptyRows.Reusable, emptyRows.Failure);
        Assert.Equal(EngineFixture.Body(learnedRows, rows), EngineFixture.Body(emptyRows, rows));
        Assert.True(emptyCards.Reusable, emptyCards.Failure);
        Assert.Equal(EngineFixture.Body(learnedCards, cards), EngineFixture.Body(emptyCards, cards));
        Assert.False(unmade.Reusable);
        Assert.Contains("empty list", unmade.Failure, StringComparison.Ordinal);
    }
}
