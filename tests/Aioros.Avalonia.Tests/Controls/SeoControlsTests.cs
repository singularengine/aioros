using System.Collections.Immutable;
using Aioros.Runtime.Rendering;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class SeoControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "SEO-05")]
    public async Task ContainersTakeSemanticElementsAsync()
    {
        var tagged = await fixture.HtmlAsync("""
            <StackPanel>
              <Border a:Web.Tag="header" Padding="4"><TextBlock Text="Top" /></Border>
              <StackPanel a:Web.Tag="nav"><TextBlock Text="Links" /></StackPanel>
              <Border a:Web.Tag="footer" Padding="4"><TextBlock Text="Bottom" /></Border>
              <Border a:Web.Tag="script" Padding="4"><TextBlock Text="Unsafe" /></Border>
            </StackPanel>
            """);
        var shell = await fixture.RenderAsync(EngineFixture.Page("Shell", "Pages/Shell/Layout.axaml"), "light", new Brand("Lumen"), new Article("Inbox"));

        Assert.Matches("<header class=\"[^\"]*\"><div[^>]*>Top</div></header>", tagged);
        Assert.Matches("<nav class=\"[^\"]*\"><div[^>]*>Links</div></nav>", tagged);
        Assert.Matches("<footer class=\"[^\"]*\"><div[^>]*>Bottom</div></footer>", tagged);
        Assert.Matches("<div class=\"[^\"]*\"><div[^>]*>Unsafe</div></div>", tagged);
        Assert.Matches("<main style=\"display:contents\"><div data-aioros-page style=\"display:contents\"><!--p--><div[^>]*>Inbox</div><!--/p--></div></main>", EngineFixture.Body(shell, new Brand("Lumen"), new Article("Inbox")));
    }

    [Fact]
    [Trait("AC", "SEO-06")]
    public async Task ItemsAreAnnouncedAsListsAsync()
    {
        var page = await fixture.CompileAsync("""
            <ItemsControl ItemsSource="{Binding Items}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """, new Catalog(new Item("one")));

        var html = EngineFixture.Body(page, new Catalog(new Item("a"), new Item("b")));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("role=\"list\"[^>]*><div role=\"listitem\" class=\"li\"><div[^>]*>a</div></div><div role=\"listitem\" class=\"li\"><div[^>]*>b</div></div>", html);
        Assert.Contains(".li{display:contents}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-31")]
    public async Task ConvertedAutomationNamesArePlannedAsync()
    {
        const string Body = """<Border Padding="4" AutomationProperties.Name="{Binding Description, Converter={x:Static m:Loud.Instance}}"><TextBlock Text="Badge" /></Border>""";
        var models = new[] { new Described("A category"), new Described(null) };
        var planned = await fixture.CompileAsync(Body, models[0]);
        var saved = Runtime.Rendering.PlansFile.Read(Runtime.Rendering.PlansFile.Write([new Runtime.Rendering.PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in models)
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Body, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        Assert.Contains("aria-label=\"A CATEGORY\"", EngineFixture.Body(planned, models[0]), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SEO-07")]
    public async Task AutomationNamesBecomeAriaLabelsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Button AutomationProperties.Name="Open menu"><a:Icon Kind="menu" /></Button>
              <Border a:Web.Href="/inbox" AutomationProperties.Name="Inbox"><a:Icon Kind="bell" /></Border>
              <TextBox AutomationProperties.Name="Search" />
            </StackPanel>
            """);

        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|\" aria-label=\"Open menu\"", html);
        Assert.Matches("<a aria-label=\"Inbox\" href=\"/inbox\"", html);
        Assert.Matches("<input [^>]*aria-label=\"Search\"", html);
        Assert.Matches("<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"", html);
    }

    [Fact]
    [Trait("AC", "SEO-42")]
    public async Task HeadingLevelsBecomeHeadingsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles><Style Selector="TextBlock.title"><Setter Property="AutomationProperties.HeadingLevel" Value="3" /></Style></StackPanel.Styles>
              <TextBlock Classes="title" Text="Third" />
              <TextBlock AutomationProperties.HeadingLevel="4" Text="Fourth" />
              <TextBlock AutomationProperties.HeadingLevel="8" Text="Eighth" />
              <Border AutomationProperties.HeadingLevel="3" Padding="4"><TextBlock Text="Boxed" /></Border>
              <TextBlock Classes="h2" Text="Classed" />
              <TextBlock AutomationProperties.HeadingLevel="0" Text="Plain" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<h3 class=\"[^\"]*\">Third</h3>", html);
        Assert.Matches("<h4 class=\"[^\"]*\">Fourth</h4>", html);
        Assert.Matches("<div role=\"heading\" aria-level=\"8\" class=\"[^\"]*\">Eighth</div>", html);
        Assert.Matches("<div role=\"heading\" aria-level=\"3\" class=\"[^\"]*\"><div[^>]*>Boxed</div></div>", html);
        Assert.Matches("<h2 class=\"[^\"]*\">Classed</h2>", html);
        Assert.Matches("<div class=\"[^\"]*\">Plain</div>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(page.Css, "h3,h4,h5,h6\\{margin:0;font-size:inherit;font-weight:inherit\\}"));
    }

    [Fact]
    [Trait("AC", "SEO-43")]
    public async Task LiveSettingsBecomeLiveRegionsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <ContentControl AutomationProperties.LiveSetting="Assertive" Padding="8" Background="#eeeeee" Content="Saved" />
              <ContentControl AutomationProperties.LiveSetting="Polite" AutomationProperties.Name="Loading"><Ellipse Width="8" Height="8" Fill="Red" /></ContentControl>
              <TextBlock AutomationProperties.LiveSetting="Polite" Text="3 results" />
              <Border AutomationProperties.LiveSetting="Off" Padding="2"><TextBlock Text="Quiet" /></Border>
            </StackPanel>
            """);

        Assert.Matches("<div role=\"alert\" class=\"[^\"]*\">(<div[^>]*>)+Saved<", html);
        Assert.Matches("<div role=\"status\" aria-label=\"Loading\" class=\"[^\"]*\"><div[^>]*></div></div>", html);
        Assert.Matches("<div role=\"status\" class=\"[^\"]*\">3 results</div>", html);
        Assert.Matches("<div class=\"[^\"]*\"><div[^>]*>Quiet</div></div>", html);
    }

    [Fact]
    [Trait("AC", "SEO-44")]
    public async Task LandmarkTypesBecomeLandmarksAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <ItemsControl AutomationProperties.LandmarkType="Navigation" AutomationProperties.Name="pagination"><Button Content="1" /></ItemsControl>
              <Panel AutomationProperties.LandmarkType="Main"><TextBlock Text="Body" /></Panel>
              <Border AutomationProperties.LandmarkType="Banner" Padding="2"><TextBlock Text="Top" /></Border>
              <Border AutomationProperties.LandmarkType="ContentInfo" Padding="2"><TextBlock Text="Bottom" /></Border>
              <Border AutomationProperties.LandmarkType="Complementary" Padding="2"><TextBlock Text="Aside" /></Border>
              <Border AutomationProperties.LandmarkType="Region" AutomationProperties.Name="Stats" Padding="2"><TextBlock Text="Region" /></Border>
              <Border AutomationProperties.LandmarkType="Search" Padding="2"><TextBlock Text="Find" /></Border>
              <Border AutomationProperties.LandmarkType="Form" Padding="2"><TextBlock Text="Form" /></Border>
              <Border AutomationProperties.LandmarkType="Navigation" a:Web.Tag="aside" Padding="2"><TextBlock Text="Tagged" /></Border>
            </StackPanel>
            """);

        Assert.Matches("<nav aria-label=\"pagination\" class=\"[^\"]*\">.*<button[^>]*>1</button>.*</nav>", html);
        Assert.Matches("<main class=\"[^\"]*\"><div[^>]*>Body</div></main>", html);
        Assert.Matches("<header class=\"[^\"]*\"><div[^>]*>Top</div></header>", html);
        Assert.Matches("<footer class=\"[^\"]*\"><div[^>]*>Bottom</div></footer>", html);
        Assert.Matches("<aside class=\"[^\"]*\"><div[^>]*>Aside</div></aside>", html);
        Assert.Matches("<section aria-label=\"Stats\" class=\"[^\"]*\"><div[^>]*>Region</div></section>", html);
        Assert.Matches("<search class=\"[^\"]*\"><div[^>]*>Find</div></search>", html);
        Assert.Matches("<div role=\"form\" class=\"[^\"]*\"><div[^>]*>Form</div></div>", html);
        Assert.Matches("<aside class=\"[^\"]*\"><div[^>]*>Tagged</div></aside>", html);
    }

    [Fact]
    [Trait("AC", "SEO-45")]
    public async Task ControlTypesBecomeRolesAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <StackPanel AutomationProperties.ControlTypeOverride="List"><Border AutomationProperties.ControlTypeOverride="ListItem" Padding="2"><TextBlock Text="Item" /></Border></StackPanel>
              <StackPanel AutomationProperties.ControlTypeOverride="Group" Orientation="Horizontal"><Button Content="A" /><Button Content="B" /></StackPanel>
              <Border AutomationProperties.ControlTypeOverride="Separator" Height="1" Background="Gray" />
              <Border AutomationProperties.AccessibilityView="Raw" Padding="2"><TextBlock Text="Hidden" /></Border>
              <Border AutomationProperties.ControlTypeOverride="Text" Padding="2"><TextBlock Text="Plain" /></Border>
            </StackPanel>
            """);

        Assert.Matches("<div role=\"list\" class=\"[^\"]*\"><div role=\"listitem\" class=\"[^\"]*\"><div[^>]*>Item</div></div></div>", html);
        Assert.Matches("<div role=\"group\" class=\"[^\"]*\"><button[^>]*>A</button><button[^>]*>B</button></div>", html);
        Assert.Matches("<div role=\"separator\" class=\"[^\"]*\"></div>", html);
        Assert.Matches("<div aria-hidden=\"true\" class=\"[^\"]*\"><div[^>]*>Hidden</div></div>", html);
        Assert.Matches("<div class=\"[^\"]*\"><div[^>]*>Plain</div></div>", html);
    }

    [Fact]
    [Trait("AC", "SEO-46")]
    public async Task LibraryThemesNameTheirElementsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Markup.Library/Keys.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <l:Key Content="Ctrl" />
              <StackPanel a:Web.Tag="ol">
                <Border a:Web.Tag="li" Padding="2"><TextBlock Text="One" /></Border>
                <Border a:Web.Tag="li" Padding="2"><TextBlock Text="Two" /></Border>
              </StackPanel>
              <TextBlock a:Web.Tag="code" Text="dotnet new aioros" />
              <TextBlock a:Web.Tag="p" Text="A paragraph." />
              <Border a:Web.Tag="p" Padding="2"><TextBlock Text="Boxed" /></Border>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<kbd class=\"[^\"]*\">(<div[^>]*>)*Ctrl(</div>)*</kbd>", html);
        Assert.Matches("<ol class=\"[^\"]*\"><li class=\"[^\"]*\"><div[^>]*>One</div></li><li class=\"[^\"]*\"><div[^>]*>Two</div></li></ol>", html);
        Assert.Matches("<code class=\"[^\"]*\">dotnet new aioros</code>", html);
        Assert.Matches("<p class=\"[^\"]*\">A paragraph\\.</p>", html);
        Assert.Matches("<div class=\"[^\"]*\"><div[^>]*>Boxed</div></div>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(page.Css, ":where\\(blockquote,p,ol,ul,li,pre\\)\\{margin:0;padding:0;list-style:none\\}:where\\(kbd,code,pre,small\\)\\{font:inherit\\}"));
    }

    [Fact]
    [Trait("AC", "SEO-47")]
    public async Task AFieldSetIsWrittenWithItsLegendAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel a:Web.Tag="fieldset" Spacing="24">
                <TextBlock a:Web.Tag="legend" Text="Payment" />
                <TextBlock Text="All transactions are secure." />
                <TextBox Text="Card" />
              </StackPanel>
              <StackPanel a:Web.Tag="fieldset">
                <TextBlock Text="Lead" />
                <TextBlock a:Web.Tag="legend" Text="Second" />
              </StackPanel>
              <StackPanel>
                <TextBlock a:Web.Tag="legend" Text="Untagged" />
              </StackPanel>
              <TextBlock a:Web.Tag="fieldset" Text="Text" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<fieldset class=\"[^\"]*\"><legend class=\"[^\"]*\">Payment</legend><div[^>]*>All transactions are secure\\.</div>", html);
        Assert.Matches("<fieldset class=\"[^\"]*\"><div[^>]*>Lead</div><div[^>]*>Second</div></fieldset>", html);
        Assert.Matches("<div[^>]*><div[^>]*>Untagged</div></div>", html);
        Assert.Matches("<div[^>]*>Text</div>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(page.Css, ":where\\(fieldset\\)\\{border:0;margin:0;padding:0;min-width:0\\}:where\\(legend\\)\\{float:left;padding:0\\}"));
    }

    [Fact]
    [Trait("AC", "SEO-48")]
    public async Task ALegendTakesPartInItsFieldSetsLayoutAsync()
    {
        const string Body = """
            <StackPanel a:Web.Tag="fieldset" Spacing="24">
              <TextBlock a:Web.Tag="legend" Margin="0,0,0,-12" Text="Payment Method" />
              <TextBlock Text="All transactions are secure and encrypted." />
              <TextBlock Text="Name on Card" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body);
        var wrong = new List<string>();
        foreach (var width in (int[])[400, 800])
        {
            var desktop = Places((await fixture.CaptureInAsync(Body, new CaptureOptions("light", width, 400))).Texts);
            var web = Places(await BrowserLayout.TextsAsync(page, width, 400));
            wrong.AddRange(desktop.SequenceEqual(web) ? [] : [$"{width}: desktop {string.Join(", ", desktop)}, web {string.Join(", ", web)}"]);
        }

        Assert.Contains("<legend", EngineFixture.Body(page), StringComparison.Ordinal);
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    private static string[] Places(ImmutableArray<LaidOutText> texts) =>
        [.. texts.OrderBy(text => text.Y).ThenBy(text => text.X).Select(text => $"{text.Text}@{Math.Round(text.X)},{Math.Round(text.Y)}")];

    [Fact]
    [Trait("AC", "SEO-49")]
    public async Task CurrentMarksAreWrittenAsAriaCurrentAsync()
    {
        const string Body = """
            <StackPanel>
              <Border a:Web.Current="page" Padding="2"><TextBlock Text="Here" /></Border>
              <TextBlock a:Web.Current="step" Text="Step" />
              <Button a:Web.Current="true" Content="Now" />
              <TextBlock a:Web.Current="bogus" Text="Bogus" />
              <ItemsControl ItemsSource="{Binding Crumbs}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:CurrentCrumb">
                    <TextBlock Text="{Binding Title}" a:Web.Current="{Binding Mark}" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """;
        CurrentTrail[] models = [new(new("Home", null), new("Docs", "page")), new(new("Home", "page"), new("Docs", null))];

        var planned = await fixture.PlansLikeAvaloniaAsync(Body, models);

        var first = EngineFixture.Body(planned, models[0]);
        var second = EngineFixture.Body(planned, models[1]);
        Assert.Matches("<div aria-current=\"page\"[^>]*><div[^>]*>Here</div></div>", first);
        Assert.Matches("<div aria-current=\"step\"[^>]*>Step</div>", first);
        Assert.Matches("<button[^>]* aria-current=\"true\"[^>]*>", first);
        Assert.Matches("<div class=\"[^\"]*\">Bogus</div>", first);
        Assert.Matches("<div class=\"[^\"]*\">Home</div>.*<div aria-current=\"page\" class=\"[^\"]*\">Docs</div>", first);
        Assert.Matches("<div aria-current=\"page\" class=\"[^\"]*\">Home</div>.*<div class=\"[^\"]*\">Docs</div>", second);
    }
}
