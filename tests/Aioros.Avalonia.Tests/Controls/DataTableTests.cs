using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed partial class DataTableTests(EngineFixture fixture)
{
    private const string Columns = """
        <TableViewColumn Width="32.84">
          <TableViewColumn.Header><CheckBox IsChecked="{Binding Source.AllPageRowsSelected}" AutomationProperties.Name="Select all" /></TableViewColumn.Header>
          <TableViewColumn.CellTemplate>
            <DataTemplate x:DataType="m:Payment"><CheckBox IsChecked="{Binding IsSelected}" AutomationProperties.Name="Select row" /></DataTemplate>
          </TableViewColumn.CellTemplate>
        </TableViewColumn>
        <TableViewColumn Width="119.66" IsVisible="{Binding StatusColumn.IsVisible}">
          <TableViewColumn.Header>Status</TableViewColumn.Header>
          <TableViewColumn.CellTemplate>
            <DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Status}" /></DataTemplate>
          </TableViewColumn.CellTemplate>
        </TableViewColumn>
        <TableViewColumn Width="271.55" IsVisible="{Binding EmailColumn.IsVisible}">
          <TableViewColumn.Header><Button Command="{Binding Source.SortCommand}" CommandParameter="email"><TextBlock Text="Email" /></Button></TableViewColumn.Header>
          <TableViewColumn.CellTemplate>
            <DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Email}" /></DataTemplate>
          </TableViewColumn.CellTemplate>
        </TableViewColumn>
        <TableViewColumn Width="100.23" IsVisible="{Binding AmountColumn.IsVisible}" HorizontalContentAlignment="Stretch">
          <TableViewColumn.Header><TextBlock Text="Amount" HorizontalAlignment="Right" /></TableViewColumn.Header>
          <TableViewColumn.CellTemplate>
            <DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Amount, Converter={x:Static m:PaymentTable.Dollars}}" HorizontalAlignment="Right" /></DataTemplate>
          </TableViewColumn.CellTemplate>
        </TableViewColumn>
        <TableViewColumn Width="65.72" Header="Id">
          <TableViewColumn.HeaderTemplate>
            <DataTemplate x:DataType="x:String"><TextBlock Text="{Binding}" FontStyle="Italic" /></DataTemplate>
          </TableViewColumn.HeaderTemplate>
          <TableViewColumn.CellTemplate>
            <DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Id}" /></DataTemplate>
          </TableViewColumn.CellTemplate>
        </TableViewColumn>
        """;

    private const string Table = $$"""
        <StackPanel Width="640">
          <StackPanel.Resources>
            <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/DataTableTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
          </StackPanel.Resources>
          <TextBox Width="384" HorizontalAlignment="Left" PlaceholderText="Filter emails..." a:Web.Delay="300" Text="{Binding Source.FilterText}" />
          <TableView ItemsSource="{Binding Source.Rows}" m:DataTable.SelectsRows="True">
            <TableView.Columns>
              {{Columns}}
            </TableView.Columns>
          </TableView>
          <TextBlock>
            <TextBlock.Text>
              <MultiBinding StringFormat="{}{0} of {1} row(s) selected.">
                <Binding Path="Source.SelectedCount" />
                <Binding Path="Source.FilteredCount" />
              </MultiBinding>
            </TextBlock.Text>
          </TextBlock>
        </StackPanel>
        """;

    private const string Visibility = """
        <TableView ItemsSource="{Binding Source.Rows}" Width="600">
          <TableView.Columns>
            <TableViewColumn Width="80" Header="Id">
              <TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Id}" /></DataTemplate></TableViewColumn.CellTemplate>
            </TableViewColumn>
            <TableViewColumn Width="120" Header="Status" IsVisible="{Binding StatusColumn.IsVisible}">
              <TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Status}" /></DataTemplate></TableViewColumn.CellTemplate>
            </TableViewColumn>
            <TableViewColumn Width="*" Header="Email" IsVisible="{Binding EmailColumn.IsVisible}">
              <TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Email}" /></DataTemplate></TableViewColumn.CellTemplate>
            </TableViewColumn>
            <TableViewColumn Width="100" Header="Amount" IsVisible="{Binding AmountColumn.IsVisible}">
              <TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Amount, Converter={x:Static m:PaymentTable.Dollars}}" /></DataTemplate></TableViewColumn.CellTemplate>
            </TableViewColumn>
          </TableView.Columns>
        </TableView>
        """;

    [Fact]
    [Trait("AC", "ITEMS-113")]
    public async Task HeadersAreWrittenAsCellsAreAsync()
    {
        var none = new PaymentTable();
        var all = new PaymentTable();
        all.Source.AllPageRowsSelected = true;

        var page = await fixture.PlansLikeAvaloniaAsync(Table, none, all);
        var html = EngineFixture.Body(page, all);
        var headers = Headers().Matches(html).Select(match => (match.Groups["class"].Value, match.Groups["content"].Value)).ToList();
        var cells = Regex.Matches(html, "<td(?<class>[^>]*)>(?<content>.*?)</td>").Select(match => (match.Groups["class"].Value, match.Groups["content"].Value)).Take(5).ToList();

        Assert.DoesNotContain("Avalonia.Controls", html, StringComparison.Ordinal);
        Assert.Equal(5, headers.Count);
        Assert.Matches("<input type=\"checkbox\" aria-label=\"Select all\" name=\"f1\" value=\"on\" checked", headers[0].Item2);
        Assert.Equal(("", "Status"), headers[1]);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"[0-9]+\\|email\"[^>]*>.*Email", headers[2].Item2);
        Assert.Equal(" class=\"r\"", headers[3].Item1);
        Assert.Matches(">Amount</div>$", headers[3].Item2);
        Assert.Matches(">Id</div>$", headers[4].Item2);
        Assert.Equal(" class=\"r\"", cells[3].Item1);
        Assert.Matches(">\\$316.00</div>$", cells[3].Item2);
        Assert.Equal("", cells[1].Item1);
        Assert.Contains("font-style:italic", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-114")]
    public async Task BoundColumnVisibilityIsFollowedAsync()
    {
        static PaymentTable Hiding(params int[] columns)
        {
            var table = new PaymentTable();
            foreach (var column in columns)
            {
                table.Source.Columns[column].IsVisible = false;
            }

            return table;
        }

        var shown = await fixture.PlansLikeAvaloniaAsync(Visibility, Hiding(), Hiding(1), Hiding(1, 3), Hiding(2));
        var hidden = await fixture.PlansLikeAvaloniaAsync(Visibility, Hiding(2), Hiding(), Hiding(3));
        var html = EngineFixture.Body(shown, Hiding(1));
        var row = Regex.Match(html, "<tbody><tr[^>]*>(.*?)</tr>").Groups[1].Value;

        Assert.Equal(3, Regex.Count(html, "<col[ >]"));
        Assert.Equal(["Id", "Email", "Amount"], Regex.Matches(html, "<th[^>]*>([^<]*)</th>").Select(match => match.Groups[1].Value));
        Assert.Equal(3, Regex.Count(row, "<td"));
        Assert.DoesNotContain("success", row, StringComparison.Ordinal);
        Assert.Contains(">Email</th>", EngineFixture.Body(hidden, Hiding()), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-108")]
    public async Task ColumnsToggledFromAMenuAreAnsweredFromThePlanAsync()
    {
        var body = "<StackPanel>" + Visibility.Replace("{Binding StatusColumn.IsVisible}", "{Binding Source.Columns[1].IsVisible}", StringComparison.Ordinal) + """
            <Button Content="Columns">
              <Button.Flyout>
                <MenuFlyout>
                  <MenuItem Header="Status" ToggleType="CheckBox" IsChecked="{Binding Source.Columns[1].IsVisible, Mode=TwoWay}" />
                </MenuFlyout>
              </Button.Flyout>
            </Button>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(body, new PaymentTable());
        var token = Regex.Match(EngineFixture.Body(page, new PaymentTable()), "value=\"([0-9]+)\\|\" role=\"menuitemcheckbox\"").Groups[1].Value;
        var shown = new PaymentTable();

        var pressed = await PlanForms.ApplyAsync(page.Plan, [shown], Form(("_btn", token + "|")), new Plans.AdapterSlotResolver(), static _ => false);

        Assert.Equal(PostStatus.Rendered, pressed);
        Assert.False(shown.Source.Columns[1].IsVisible);
        Assert.DoesNotContain(">Status</th>", EngineFixture.Body(page, shown), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-244")]
    public async Task ColumnsShownThroughAnIndexArePlannedAndToggledAsync()
    {
        var body = "<StackPanel>" + Visibility.Replace("{Binding StatusColumn.IsVisible}", "{Binding Source.Columns[1].IsVisible}", StringComparison.Ordinal)
            .Replace("{Binding EmailColumn.IsVisible}", "{Binding Source.Columns[2].IsVisible}", StringComparison.Ordinal)
            .Replace("{Binding AmountColumn.IsVisible}", "{Binding Source.Columns[3].IsVisible}", StringComparison.Ordinal) + """
            <Button Content="Columns">
              <Button.Flyout>
                <MenuFlyout>
                  <MenuItem Header="Status" ToggleType="CheckBox" IsChecked="{Binding Source.Columns[1].IsVisible, Mode=TwoWay}" />
                  <MenuItem Header="Email" ToggleType="CheckBox" IsChecked="{Binding Source.Columns[2].IsVisible, Mode=TwoWay}" />
                </MenuFlyout>
              </Button.Flyout>
            </Button>
            </StackPanel>
            """;
        var shown = new PaymentTable();
        var hiding = new PaymentTable();
        hiding.Source.Columns[2].IsVisible = false;

        var page = await fixture.PlansLikeAvaloniaAsync(body, shown, hiding);
        var html = EngineFixture.Body(page, shown);
        var status = Regex.Match(html, "value=\"([0-9]+)\\|\" role=\"menuitemcheckbox\" aria-checked=\"true\"[^>]*>(?:<svg.*?</svg>)?<span class=\"mh\">Status</span>");
        Assert.True(status.Success, html);
        var posted = await fixture.PostAsync(body, shown, ("_btn", status.Groups[1].Value + "|"));
        var answer = EngineFixture.Body(posted.Page!, shown);

        Assert.Contains(">Status</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Email</th>", EngineFixture.Body(page, hiding), StringComparison.Ordinal);
        Assert.Matches("role=\"menuitemcheckbox\" aria-checked=\"false\"[^>]*>(?:<svg.*?</svg>)?<span class=\"mh\">Email</span>", EngineFixture.Body(page, hiding));
        Assert.False(shown.Source.Columns[1].IsVisible);
        Assert.DoesNotContain(">Status</th>", answer, StringComparison.Ordinal);
        Assert.Contains(">Email</th>", answer, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-115")]
    public async Task ARowTakesTheLookOfItsItemsFlagAsync()
    {
        var none = new PaymentTable();
        var some = new PaymentTable();
        some.Source.Items[1].IsSelected = true;
        some.Source.Items[3].IsSelected = true;
        var all = new PaymentTable();
        all.Source.AllPageRowsSelected = true;

        var page = await fixture.PlansLikeAvaloniaAsync(Table, none, some, all);
        var rows = Regex.Matches(EngineFixture.Body(page, some), "<tr class=\"([^\"]+)\">").Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(5, rows.Count);
        Assert.Equal([rows[0], rows[1], rows[0], rows[1], rows[0]], rows);
        Assert.NotEqual(rows[0], rows[1]);
        Assert.Matches($"\\.{rows[1]}\\{{background:#f5f5f5;", page.Css);
        Assert.Matches($"\\.{rows[0]}\\{{background:transparent;", page.Css);
        Assert.Matches($"\\.{rows[0]}:hover\\{{background:rgba\\(245,245,245,0.502\\);", page.Css);
        Assert.Equal(0, none.Source.SelectedCount);
    }

    [Fact]
    [Trait("AC", "ITEMS-116")]
    public async Task ATablesTwoWayFieldsAndADelayedTextPostOnChangeAsync()
    {
        var html = await fixture.HtmlAsync(Table, new PaymentTable());
        var plain = await fixture.HtmlAsync("""
            <StackPanel>
              <TextBox Text="{Binding Source.FilterText}" />
              <TableView ItemsSource="{Binding Source.Rows}">
                <TableView.Columns>
                  <TableViewColumn Header="Email"><TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Email}" /></DataTemplate></TableViewColumn.CellTemplate></TableViewColumn>
                </TableView.Columns>
              </TableView>
            </StackPanel>
            """, new PaymentTable());

        Assert.Matches("<div data-change class=\"[^\"]+\"><table", html);
        Assert.Matches("<input type=\"text\" name=\"f0\" data-change data-delay=\"300\"", html);
        Assert.DoesNotContain("data-change", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("data-delay", plain, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-117")]
    public async Task ATablesChangedFieldIsBoundAfterTheOthersAsync()
    {
        static PaymentTable SecondSelected()
        {
            var table = new PaymentTable();
            table.Source.Items[1].IsSelected = true;
            return table;
        }

        var row = SecondSelected();
        var before = row.Source.Items.Select(payment => payment.Changes).ToList();
        var checkedRow = await fixture.PostAsync(Table, row, ("_chg", "5"), ("f0", ""), ("f3", "on"), ("f5", "on"));
        var header = SecondSelected();
        await fixture.PostAsync(Table, header, ("_chg", "1"), ("f0", ""), ("f1", "on"), ("f3", "on"));
        var filter = SecondSelected();
        var filtered = await fixture.PostAsync(Table, filter, ("_chg", "0"), ("f0", "abe"), ("f3", "on"));
        var rowHtml = EngineFixture.Body(checkedRow.Page!, row);
        var filteredHtml = EngineFixture.Body(filtered.Page!, filter);
        var rows = Regex.Matches(rowHtml, "<tr class=\"([^\"]+)\">").Select(match => match.Groups[1].Value).ToList();

        Assert.Equal([false, true, false, true, false], row.Source.Items.Select(payment => payment.IsSelected));
        Assert.Null(row.Source.AllPageRowsSelected);
        Assert.Equal([0, 0, 0, 1, 0], row.Source.Items.Select((payment, index) => payment.Changes - before[index]));
        Assert.Contains("2 of 5 row(s) selected.", rowHtml, StringComparison.Ordinal);
        Assert.Equal([rows[0], rows[1], rows[0], rows[1], rows[0]], rows);
        Assert.True(header.Source.Items.All(payment => payment.IsSelected));
        Assert.Equal([false, true, false, false, false], filter.Source.Items.Select(payment => payment.IsSelected));
        Assert.Single(Regex.Matches(filteredHtml, "<tr class="));
        Assert.Contains("Abe45@example.com", filteredHtml, StringComparison.Ordinal);
        Assert.Contains("1 of 1 row(s) selected.", filteredHtml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-299")]
    public async Task APlannedPostBindsTheChangedFieldAfterTheOthersAsync()
    {
        static PaymentTable SecondSelected()
        {
            var table = new PaymentTable();
            table.Source.Items[1].IsSelected = true;
            return table;
        }

        var page = await fixture.CompileAsync(Table, SecondSelected());
        var header = SecondSelected();
        var row = SecondSelected();

        var all = await PlanForms.ApplyAsync(page.Plan, [header], Form(("_chg", "1"), ("f0", ""), ("f1", "on"), ("f3", "on")), new Plans.AdapterSlotResolver());
        var one = await PlanForms.ApplyAsync(page.Plan, [row], Form(("_chg", "5"), ("f0", ""), ("f3", "on"), ("f5", "on")), new Plans.AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal(PostStatus.Rendered, all);
        Assert.Equal(PostStatus.Rendered, one);
        Assert.True(header.Source.Items.All(payment => payment.IsSelected));
        Assert.Equal([false, true, false, true, false], row.Source.Items.Select(payment => payment.IsSelected));
    }

    private static ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        fields.ToImmutableDictionary(field => field.Key, field => field.Value);

    [Fact]
    [Trait("AC", "ITEMS-119")]
    public async Task StarColumnsShareTheirRoomAsPercentagesAsync()
    {
        static string Table(string visible) => $$"""
            <TableView ItemsSource="{Binding Source.Rows}" Width="592">
              <TableView.Columns>
                <TableViewColumn Header="Invoice" Width="100"><TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Id}" /></DataTemplate></TableViewColumn.CellTemplate></TableViewColumn>
                <TableViewColumn Header="Status" Width="128*"><TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Status}" /></DataTemplate></TableViewColumn.CellTemplate></TableViewColumn>
                <TableViewColumn Header="Method" Width="202*"{{visible}}><TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Email}" /></DataTemplate></TableViewColumn.CellTemplate></TableViewColumn>
                <TableViewColumn Header="Amount" Width="162*"><TableViewColumn.CellTemplate><DataTemplate x:DataType="m:Payment"><TextBlock Text="{Binding Amount}" /></DataTemplate></TableViewColumn.CellTemplate></TableViewColumn>
              </TableView.Columns>
            </TableView>
            """;

        var plain = await fixture.HtmlAsync(Table(string.Empty), new PaymentTable());
        var bound = await fixture.PlansLikeAvaloniaAsync(Table(" IsVisible=\"{Binding EmailColumn.IsVisible}\""), new PaymentTable());
        var grid = await fixture.HtmlAsync("""
            <DataGrid ItemsSource="{Binding Products}" IsReadOnly="True" HeadersVisibility="Column">
              <DataGrid.Columns>
                <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" x:DataType="m:Product" />
                <DataGridTextColumn Header="Stock" Binding="{Binding Stock}" Width="80" x:DataType="m:Product" />
              </DataGrid.Columns>
            </DataGrid>
            """, new Inventory(new Product("Pens", 12)));

        Assert.Contains("<colgroup><col style=\"width:100px\"><col style=\"width:26.016%\"><col style=\"width:41.057%\"><col style=\"width:32.927%\"></colgroup>", plain, StringComparison.Ordinal);
        Assert.Contains("<colgroup style=\"--ts:calc(0 + 128 + 202 + 162)\"><col style=\"width:100px\"><col style=\"width:calc(100% * 128 / var(--ts))\">", EngineFixture.Body(bound, new PaymentTable()), StringComparison.Ordinal);
        Assert.Contains("<colgroup><col style=\"width:100%\"><col style=\"width:80px\"></colgroup>", grid, StringComparison.Ordinal);
        Assert.DoesNotContain("100% -", plain + grid, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-120")]
    public async Task HeadersTakeTheirOwnPaddingAsync()
    {
        var themed = await fixture.CompileAsync(Table, new PaymentTable());
        var fluent = await fixture.CompileAsync(Visibility, new PaymentTable());
        var rule = Regex.Match(themed.Css, "(?m)^\\.(tt[0-9a-f]+)\\{.*$").Value;

        Assert.Matches(" th\\{height:40px;background:transparent;padding:0px 8px 0px 8px\\}", rule);
        Assert.Contains(" th:is(:first-child,:last-child){padding-left:8px;padding-right:8px}", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("th:is(", Regex.Match(fluent.Css, "(?m)^\\.(tt[0-9a-f]+)\\{.*$").Value, StringComparison.Ordinal);
    }

    [GeneratedRegex("<th(?<class>[^>]*)>(?<content>.*?)</th>")]
    private static partial Regex Headers();
}
