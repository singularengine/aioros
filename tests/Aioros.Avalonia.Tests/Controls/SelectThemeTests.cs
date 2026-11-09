using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class SelectThemeTests(EngineFixture fixture)
{
    private const string Theme = """
        <ControlTheme x:Key="Select" TargetType="ComboBox">
          <Setter Property="Background" Value="Transparent" />
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="6" />
          <Setter Property="Padding" Value="12,8,8,8" />
          <Setter Property="PlaceholderForeground" Value="#737373" />
          <Setter Property="Template">
            <ControlTemplate>
              <Grid ColumnDefinitions="*,Auto">
                <Border Name="Background" Grid.ColumnSpan="2" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" BoxShadow="0 1 2 0 #0d000000" />
                <ContentControl Name="ContentPresenter" Margin="{TemplateBinding Padding}" Content="{TemplateBinding SelectionBoxItem}" />
                <Path Name="DropDownGlyph" Grid.Column="1" Width="16" Height="16" Margin="0,0,12,0" Data="M4,6 L8,10 L12,6" Stretch="None"
                      Stroke="#737373" StrokeThickness="1.333" VerticalAlignment="Center" />
                <Popup Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen, Mode=TwoWay}">
                  <Border Name="PopupBorder" Background="#ffffff" BorderBrush="#e5e5e5" BorderThickness="1" CornerRadius="6" BoxShadow="0 4 6 -1 #1a000000">
                    <ItemsPresenter Name="PART_ItemsPresenter" Margin="4" />
                  </Border>
                </Popup>
              </Grid>
            </ControlTemplate>
          </Setter>
          <Style Selector="^:pointerover /template/ Border#Background"><Setter Property="Background" Value="#f5f5f5" /></Style>
          <Style Selector="^:focus-visible /template/ Border#Background">
            <Setter Property="BorderBrush" Value="#a1a1a1" />
            <Setter Property="BoxShadow" Value="0 0 0 3 #80a1a1a1" />
          </Style>
        </ControlTheme>
        <ControlTheme x:Key="{x:Type ComboBoxItem}" TargetType="ComboBoxItem">
          <Setter Property="Padding" Value="8,6,32,6" />
          <Setter Property="CornerRadius" Value="4" />
          <Setter Property="Template">
            <ControlTemplate>
              <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}"
                                Content="{TemplateBinding Content}" Padding="{TemplateBinding Padding}" TextBlock.LineHeight="20" />
            </ControlTemplate>
          </Setter>
          <Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /></Style>
        </ControlTheme>
        """;

    [Fact]
    [Trait("AC", "ITEMS-93")]
    public async Task SelectsAreDrawnAsTheirThemeAsync()
    {
        var page = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><x:String>Apple</x:String></ComboBox>"""));

        var rules = Rules(page);
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*border-color:#e5e5e5;[^}]*border-radius:6px 6px 6px 6px;padding:8px 35px 8px 12px;background:transparent;box-shadow:0px 1px 2px 0px rgba\\(0,0,0,0\\.051\\);background-image:url\\(\"data:image/svg\\+xml,[^\"]*stroke='%23737373'[^\"]*\"\\);background-repeat:no-repeat;background-origin:border-box;background-position:right 12px center;background-size:16px 16px;appearance:none;", rules);
        Assert.Contains(":hover{background-color:#f5f5f5!important;}", rules, StringComparison.Ordinal);
        Assert.Contains(":focus-visible{border-color:#a1a1a1!important;box-shadow:0px 0px 0px 3px rgba(161,161,161,0.502)!important;}", rules, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-94")]
    public async Task SelectsWriteTheirPlaceholderAndDisabledStateAsync()
    {
        const string Body = """
            <StackPanel>
              <ComboBox Theme="{StaticResource Select}" PlaceholderText="Pick a fruit" SelectedItem="{Binding Fruit}"><x:String>Apple</x:String><x:String>Pear</x:String></ComboBox>
              <ComboBox Theme="{StaticResource Select}" IsEnabled="False" SelectedIndex="0"><x:String>Off</x:String></ComboBox>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Page(Body), new Basket { Fruit = null! }, new Basket { Fruit = "Pear" });

        Assert.Contains("<option value=\"\" disabled hidden selected>Pick a fruit</option><option value=\"0\">Apple</option><option value=\"1\">Pear</option>", EngineFixture.Body(page, new Basket { Fruit = null! }), StringComparison.Ordinal);
        Assert.Contains("<option value=\"\" disabled hidden>Pick a fruit</option><option value=\"0\">Apple</option><option value=\"1\" selected>Pear</option>", EngineFixture.Body(page, new Basket { Fruit = "Pear" }), StringComparison.Ordinal);
        Assert.Matches("<select name=\"f1\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" disabled><option value=\"0\" selected>Off</option></select>", EngineFixture.Body(page, new Basket()));
        Assert.Matches(":has\\(>option\\[value=\"\"\\]:checked\\)\\{color:#737373\\}", page.Css);
    }

    [Fact]
    [Trait("AC", "ITEMS-95")]
    public async Task OptionsShowWhatTheirTemplatesWriteAsync()
    {
        const string Body = """
            <StackPanel>
              <ComboBox ItemsSource="{Binding Categories}" SelectedItem="{Binding Category}">
                <ComboBox.ItemTemplate><DataTemplate x:DataType="m:Category"><TextBlock Text="{Binding Name}" /></DataTemplate></ComboBox.ItemTemplate>
              </ComboBox>
              <ComboBox ItemsSource="{Binding Categories}" DisplayMemberBinding="{Binding Name}" />
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Assignment(0, new Category("Home"), new Category("Errands")), new Assignment(1, new Category("Work"), new Category("Gym")));

        var html = EngineFixture.Body(page, new Assignment(1, new Category("Work"), new Category("Gym")));
        Assert.Contains("<option value=\"0\">Work</option><option value=\"1\" selected>Gym</option>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"0\">Work</option><option value=\"1\">Gym</option></select>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Models.Category", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-96")]
    public async Task DisabledItemsAndSeparatorsAreWrittenAsSuchAsync()
    {
        var html = EngineFixture.Body(await fixture.CompileAsync("""
            <ComboBox SelectedIndex="1">
              <ComboBoxItem Content="Fruits" IsEnabled="False" />
              <ComboBoxItem Content="Apple" />
              <ComboBoxItem IsEnabled="False" />
              <ComboBoxItem><TextBlock Text="Carrot" /></ComboBoxItem>
            </ComboBox>
            """));

        Assert.Contains("<option value=\"0\" disabled>Fruits</option><option value=\"1\" selected>Apple</option><hr><option value=\"3\">Carrot</option>", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-97")]
    public async Task CustomizableSelectListsTakeTheThemesPopupAsync()
    {
        var page = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /><ComboBoxItem Content="Pear" /></ComboBox>"""));

        var picker = Regex.Match(page.Css, "@supports \\(appearance:base-select\\)\\{[^\\n]*\\}\\n").Value;
        Assert.Matches("\\.(pk[0-9a-f]+)\\.\\1,\\.\\1::picker\\(select\\)\\{appearance:base-select\\}", picker);
        Assert.Matches("::picker-icon\\{display:none\\}", picker);
        Assert.Matches("::picker\\(select\\)\\{background:#ffffff;border-color:#e5e5e5;border-width:1px 1px 1px 1px;border-style:solid;border-radius:6px 6px 6px 6px;padding:4px 4px 4px 4px;box-shadow:0px 4px 6px -1px rgba\\(0,0,0,0\\.102\\);", picker);
        Assert.Matches(" option\\{border-width:0;padding:6px 32px 6px 8px;border-radius:4px 4px 4px 4px;line-height:20px;", picker);
        Assert.Matches(" option:hover\\{background:#f5f5f5;\\}", picker);
    }

    [Fact]
    [Trait("AC", "ITEMS-135")]
    public async Task ScrollButtonsScrollTheListWhilePointedAtAsync()
    {
        var page = await ScrollingAsync();
        const string Top = "() => { const u = document.querySelector('[data-aioros-scroller=up]'); return Math.round(document.querySelector('option').getBoundingClientRect().top - (getComputedStyle(u).display === 'block' ? u.offsetHeight : 0)) }";

        var found = await BrowserLayout.ClockedAsync(page, 600, 500, [Aioros.Targets.Ssr.Documents.ScrollerScript.Bytes], async tab =>
        {
            var steps = new List<string>();
            async Task<bool> ShownAsync(string which, bool shown)
            {
                var condition = $"() => (getComputedStyle(document.querySelector('[data-aioros-scroller={which}]')).display === 'block') === {(shown ? "true" : "false")}";
                for (var attempt = 0; attempt < 2000; attempt++)
                {
                    if (await tab.EvaluateAsync<bool>(condition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task HoverAsync(string which)
            {
                var box = await tab.Locator($"[data-aioros-scroller={which}]").BoundingBoxAsync();
                await tab.Mouse.MoveAsync(box!.X + (box.Width / 2), box.Y + (box.Height / 2));
            }

            await tab.ClickAsync("select");
            steps.Add($"start down {await ShownAsync("down", shown: true)} up {!await ShownAsync("up", shown: false)}");
            var start = await tab.EvaluateAsync<int>(Top);
            await HoverAsync("down");
            await tab.Clock.RunForAsync(50);
            var first = await tab.EvaluateAsync<int>(Top);
            steps.Add($"scrolled {start - first} up {await ShownAsync("up", shown: true)}");
            var shifted = await tab.EvaluateAsync<int>(Top);
            await tab.Clock.RunForAsync(100);
            steps.Add($"two more {shifted - await tab.EvaluateAsync<int>(Top)}");
            await tab.Mouse.MoveAsync(590, 490);
            var left = await tab.EvaluateAsync<int>(Top);
            await tab.Clock.RunForAsync(200);
            steps.Add($"left {left - await tab.EvaluateAsync<int>(Top)}");
            await HoverAsync("down");
            await tab.Clock.RunForAsync(3000);
            steps.Add($"end down {!await ShownAsync("down", shown: false)}");
            var end = await tab.EvaluateAsync<int>(Top);
            await tab.Clock.RunForAsync(200);
            steps.Add($"stopped {end - await tab.EvaluateAsync<int>(Top)}");
            await HoverAsync("up");
            await tab.Clock.RunForAsync(50);
            steps.Add($"back {await tab.EvaluateAsync<int>(Top) > end}");
            return steps;
        });

        var placeholder = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + Scrolling(Theme) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" PlaceholderText=\"Pick one\" Width=\"200\">"
            + string.Concat(Enumerable.Range(0, 12).Select(index => $"<ComboBoxItem Content=\"Item {index}\" />")) + "</ComboBox></StackPanel>");
        var unselected = await BrowserLayout.ClockedAsync(placeholder, 600, 500, [Aioros.Targets.Ssr.Documents.ScrollerScript.Bytes], async tab =>
        {
            await tab.ClickAsync("select");
            var shown = false;
            for (var attempt = 0; attempt < 2000 && !shown; attempt++)
            {
                shown = await tab.EvaluateAsync<bool>("() => getComputedStyle(document.querySelector('[data-aioros-scroller=down]')).display === 'block'");
            }

            const string Shown = "() => { const u = document.querySelector('[data-aioros-scroller=up]'); return Math.round([...document.querySelectorAll('option')].find(o => o.offsetHeight > 0).getBoundingClientRect().top - (getComputedStyle(u).display === 'block' ? u.offsetHeight : 0)) }";
            var start = await tab.EvaluateAsync<int>(Shown);
            var box = await tab.Locator("[data-aioros-scroller=down]").BoundingBoxAsync();
            await tab.Mouse.MoveAsync(box!.X + (box.Width / 2), box.Y + (box.Height / 2));
            await tab.Clock.RunForAsync(50);
            return start - await tab.EvaluateAsync<int>(Shown);
        });

        Assert.Equal(["start down True up False", "scrolled 32 up True", "two more 64", "left 0", "end down False", "stopped 0", "back True"], found);
        Assert.Equal(32, unselected);
    }

    private static string CosmoLikeTheme() => Theme
        .Replace("<Popup Name=\"PART_Popup\" ", "<Popup Name=\"PART_Popup\" Placement=\"Bottom\" VerticalOffset=\"4\" ", StringComparison.Ordinal)
        .Replace("<Border Name=\"PopupBorder\" ", "<Border Name=\"PopupBorder\" Margin=\"8\" ", StringComparison.Ordinal)
        .Replace("<ContentPresenter Name=\"PART_ContentPresenter\" Background", "<Panel><ContentPresenter Name=\"PART_ContentPresenter\" Background", StringComparison.Ordinal)
        .Replace("TextBlock.LineHeight=\"20\" />", "TextBlock.LineHeight=\"20\" /><Path Name=\"Indicator\" Width=\"16\" Height=\"16\" Margin=\"0,0,7,0\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Center\" Data=\"M13.333,4 L6,11.333 L2.667,8\" Stroke=\"#0a0a0a\" StrokeThickness=\"1.333\" IsVisible=\"False\" /></Panel>", StringComparison.Ordinal)
        .Replace("""<Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /></Style>""", """
            <Style Selector="^:pointerover"><Setter Property="Background" Value="#f5f5f5" /></Style>
            <Style Selector="^:selected /template/ Path#Indicator"><Setter Property="IsVisible" Value="True" /></Style>
            <Style Selector="^.label"><Setter Property="FontSize" Value="12" /><Setter Property="Foreground" Value="#737373" /><Setter Property="Padding" Value="8,6" /><Setter Property="IsEnabled" Value="False" /><Setter Property="Opacity" Value="1" /></Style>
            <Style Selector="^.label /template/ ContentPresenter#PART_ContentPresenter"><Setter Property="TextBlock.LineHeight" Value="16" /></Style>
            """, StringComparison.Ordinal);

    private Task<CompiledPage> CosmoLikeAsync() => fixture.CompileAsync("<StackPanel Margin=\"200,40,0,0\"><StackPanel.Resources>" + CosmoLikeTheme() + """</StackPanel.Resources><ComboBox Theme="{StaticResource Select}" SelectedIndex="1" Width="120" HorizontalAlignment="Left"><ComboBoxItem Classes="label" Content="Fruits" /><ComboBoxItem Content="Banana split with cream" /><ComboBoxItem Content="Pear" /></ComboBox></StackPanel>""");

    [Fact]
    [Trait("AC", "ITEMS-143")]
    public async Task ADisabledSelectTakesItsPartsDisabledColorsAsync()
    {
        var theme = Theme
            .Replace("<ContentControl Name=\"ContentPresenter\" ", "<TextBlock Name=\"PlaceholderTextBlock\" Margin=\"{TemplateBinding Padding}\" Text=\"{TemplateBinding PlaceholderText}\" Foreground=\"{TemplateBinding PlaceholderForeground}\" /><ContentControl Name=\"ContentPresenter\" ", StringComparison.Ordinal)
            .Replace("""<Style Selector="^:pointerover /template/ Border#Background">""", """
                <Style Selector="^:disabled /template/ ContentControl#ContentPresenter"><Setter Property="Foreground" Value="#a0a0a0" /></Style>
                <Style Selector="^:disabled /template/ TextBlock#PlaceholderTextBlock"><Setter Property="Foreground" Value="#c0c0c0" /></Style>
                <Style Selector="^:pointerover /template/ Border#Background">
                """, StringComparison.Ordinal);
        var page = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + theme + """
            </StackPanel.Resources>
            <ComboBox Theme="{StaticResource Select}" SelectedIndex="0" IsEnabled="False" Foreground="#0a0a0a"><ComboBoxItem Content="Pear" /></ComboBox>
            <ComboBox Theme="{StaticResource Select}" PlaceholderText="Pick" IsEnabled="False" Foreground="#0a0a0a"><ComboBoxItem Content="Pear" /></ComboBox>
            <ComboBox Theme="{StaticResource Select}" SelectedIndex="0" Foreground="#0a0a0a"><ComboBoxItem Content="Pear" /></ComboBox>
            </StackPanel>
            """);

        var colors = await BrowserLayout.EvaluateAsync(page, 600, 400, "() => [...document.querySelectorAll('select')].map(s => getComputedStyle(s).color).join(' / ')");

        Assert.Equal("rgb(160, 160, 160) / rgb(192, 192, 192) / rgb(10, 10, 10)", colors);
    }

    [Fact]
    [Trait("AC", "ITEMS-137")]
    public async Task ThePickerStandsWhereThePopupIsPlacedAsync()
    {
        var page = await CosmoLikeAsync();

        var placed = await BrowserLayout.ClickAndEvaluateAsync(page, 800, 500, "select", "() => document.querySelector('select').matches(':open')", "() => { const s = document.querySelector('select').getBoundingClientRect(), o = document.querySelectorAll('option')[0].getBoundingClientRect(); return Math.round(Math.abs((o.left + o.right) / 2 - (s.left + s.right) / 2)) + ' ' + Math.round(o.top - s.bottom) }");

        var overlapping = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + CosmoLikeTheme().Replace("VerticalOffset=\"4\"", "VerticalOffset=\"-4\"", StringComparison.Ordinal) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" SelectedIndex=\"0\"><ComboBoxItem Content=\"Pear\" /></ComboBox></StackPanel>");
        var nested = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + CosmoLikeTheme().Replace("VerticalOffset=\"4\"", "VerticalOffset=\"-4\"", StringComparison.Ordinal).Replace("<Border Name=\"PopupBorder\" ", "<Panel><Border Name=\"PopupBorder\" ", StringComparison.Ordinal).Replace("</Popup>", "</Panel></Popup>", StringComparison.Ordinal) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" SelectedIndex=\"0\"><ComboBoxItem Content=\"Pear\" /></ComboBox></StackPanel>");

        Assert.Contains("position-area:bottom;margin-top:4px;position-try-fallbacks:flip-block;", page.Css, StringComparison.Ordinal);
        Assert.Equal("0 9", placed);
        Assert.Contains("position-area:bottom;margin-top:-4px;position-try-fallbacks:flip-block;", overlapping.Css, StringComparison.Ordinal);
        Assert.Contains("position-area:bottom;margin-top:4px;position-try-fallbacks:flip-block;", nested.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-142")]
    public async Task ThePickerKeepsItsCollisionPaddingFromTheViewportsEdgesAsync()
    {
        static string Placed(string padding, string alignment) => "<StackPanel><StackPanel.Resources>" + Theme.Replace("<Popup Name=\"PART_Popup\" ", $"<Popup Name=\"PART_Popup\" Placement=\"Bottom\" a:Web.CollisionPadding=\"{padding}\" ", StringComparison.Ordinal)
            + $"</StackPanel.Resources><ComboBox Theme=\"{{StaticResource Select}}\" SelectedIndex=\"0\" Width=\"80\" Margin=\"2,40\" HorizontalAlignment=\"{alignment}\"><ComboBoxItem Content=\"Banana split with whipped cream\" /><ComboBoxItem Content=\"Pear\" /></ComboBox></StackPanel>";
        const string Edges = "() => { const o = document.querySelector('option').getBoundingClientRect(); return Math.round(o.left - 5) + ' ' + Math.round(innerWidth - o.right - 5) }";
        var found = new List<string>();
        foreach (var (padding, alignment) in new[] { ("10", "Left"), ("10", "Right"), ("0", "Left") })
        {
            var page = await fixture.CompileAsync(Placed(padding, alignment));
            found.Add(await BrowserLayout.ClickAndEvaluateAsync(page, 600, 400, "select", "() => document.querySelector('select').matches(':open')", Edges));
        }

        Assert.Equal(["10", "10", "0"], [found[0].Split(' ')[0], found[1].Split(' ')[1], found[2].Split(' ')[0]]);
    }

    [Fact]
    [Trait("AC", "ITEMS-138")]
    public async Task DisabledOptionsTakeTheDisabledItemsLookAsync()
    {
        var page = await CosmoLikeAsync();
        var plain = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /></ComboBox>"""));

        var look = await BrowserLayout.ClickAndEvaluateAsync(page, 800, 500, "select", "() => document.querySelector('select').matches(':open')", "() => { const c = getComputedStyle(document.querySelector('option:disabled')); return [c.fontSize, c.lineHeight, c.paddingTop, c.paddingRight, c.color, c.opacity].join(' ') }");

        Assert.Equal("12px 16px 6px 8px rgb(115, 115, 115) 1", look);
        Assert.DoesNotContain("option:disabled", plain.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-139")]
    public async Task TheCheckMarkIsTheThemesSelectedPartAsync()
    {
        var page = await CosmoLikeAsync();
        var scrolling = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + Scrolling(CosmoLikeTheme()) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" SelectedIndex=\"3\" Width=\"200\">"
            + string.Concat(Enumerable.Range(0, 12).Select(index => $"<ComboBoxItem Content=\"Item {index}\" />")) + "</ComboBox></StackPanel>");
        var plain = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /></ComboBox>"""));
        const string Mark = "() => { const c = getComputedStyle(document.querySelector('option:checked'), '::checkmark'); return [c.display, c.position, c.right, c.top, c.width, c.height, c.content.slice(0, 4)].join(' ') }";

        var drawn = await BrowserLayout.ClickAndEvaluateAsync(page, 800, 500, "select", "() => document.querySelector('select').matches(':open')", Mark);
        var behindButtons = await BrowserLayout.ClickAndEvaluateAsync(scrolling, 800, 500, "select", "() => document.querySelector('select').matches(':open')", Mark);
        var none = await BrowserLayout.ClickAndEvaluateAsync(plain, 800, 500, "select", "() => document.querySelector('select').matches(':open')", Mark);

        Assert.Equal("block absolute 7px 8px 16px 16px url(", drawn);
        Assert.Equal(drawn, behindButtons);
        Assert.StartsWith("none ", none, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ITEMS-136")]
    public async Task TheCustomizableSelectOutweighsTheFieldsOwnClassAsync()
    {
        var page = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /><ComboBoxItem Content="Pear" /></ComboBox>"""));

        var found = await BrowserLayout.ClickAndEvaluateAsync(page, 600, 400, "select", "() => document.querySelector('select').matches(':open')", "() => getComputedStyle(document.querySelector('select')).appearance + ' ' + getComputedStyle(document.querySelector('select'), '::picker(select)').backgroundColor");

        Assert.Equal("base-select rgb(255, 255, 255)", found);
    }

    [Fact]
    [Trait("AC", "ITEMS-140")]
    public async Task ANativeListKeepsTheBrowsersSelectAsync()
    {
        var page = await fixture.CompileAsync(Page("""<StackPanel Spacing="8"><ComboBox Theme="{StaticResource Select}" a:Web.NativeList="True" SelectedIndex="0" HorizontalAlignment="Left"><ComboBoxItem Content="Todo" /><ComboBoxItem Content="Done and dusted" /></ComboBox><ComboBox Theme="{StaticResource Select}" SelectedIndex="0" HorizontalAlignment="Left"><ComboBoxItem Content="Todo" /><ComboBoxItem Content="Done and dusted" /></ComboBox></StackPanel>"""));

        var found = await BrowserLayout.EvaluateAsync(page, 600, 400, "() => { const [n, c] = document.querySelectorAll('select'); return getComputedStyle(n).appearance + ' ' + getComputedStyle(c).appearance + ' ' + (n.getBoundingClientRect().width > c.getBoundingClientRect().width) }");

        var marked = System.Text.RegularExpressions.Regex.Matches(EngineFixture.Body(page), "<select[^>]* class=\"([^\"]+)\"")[0].Groups[1].Value;
        Assert.DoesNotContain(" pk", " " + marked, StringComparison.Ordinal);
        Assert.Equal("none base-select true", found);
    }

    [Fact]
    [Trait("AC", "MOTION-16")]
    public async Task CustomizableSelectListsEnterAndLeaveAsTheThemesPopupAsync()
    {
        var entering = Theme.Replace("<Border Name=\"PopupBorder\" Background=\"#ffffff\"", "<Border Name=\"PopupBorder\" m:Fade.Enter=\"True\" m:Fade.Lift=\"4\" Background=\"#ffffff\"", StringComparison.Ordinal)
            .Replace("<ItemsPresenter Name=\"PART_ItemsPresenter\" Margin=\"4\" />", "<Border.Transitions><Transitions><DoubleTransition Property=\"Opacity\" Duration=\"0:0:0.15\" /><TransformOperationsTransition Property=\"RenderTransform\" Duration=\"0:0:0.15\" /></Transitions></Border.Transitions><ItemsPresenter Name=\"PART_ItemsPresenter\" Margin=\"4\" />", StringComparison.Ordinal)
            .Replace("<Style Selector=\"^:pointerover /template/ Border#Background\">", "<Style Selector=\"^ /template/ Border#PopupBorder:closing\"><Setter Property=\"Opacity\" Value=\"0\" /></Style><Style Selector=\"^:pointerover /template/ Border#Background\">", StringComparison.Ordinal);
        var page = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + entering + """</StackPanel.Resources><ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /><ComboBoxItem Content="Pear" /></ComboBox></StackPanel>""");

        var picker = Regex.Match(page.Css, "@supports \\(appearance:base-select\\)\\{[^\\n]*\\}\\n").Value;
        var name = Regex.Match(picker, "\\.(pk[0-9a-f]+)::picker\\(select\\)").Groups[1].Value;
        Assert.Contains($".{name}::picker(select){{transition:opacity 150ms linear 0ms,transform 150ms linear 0ms,display 150ms allow-discrete,overlay 150ms allow-discrete;opacity:0;}}", picker, StringComparison.Ordinal);
        Assert.Contains($".{name}:open::picker(select){{opacity:1;transform:none;}}", picker, StringComparison.Ordinal);
        Assert.Contains($"@starting-style{{.{name}:open::picker(select){{opacity:0;transform:matrix(1,0,0,1,0,-4);}}}}", picker, StringComparison.Ordinal);
        Assert.Contains($"@media (prefers-reduced-motion:reduce){{.{name}::picker(select){{transition:none!important}}}}", picker, StringComparison.Ordinal);
    }

    private static string Scrolling(string theme) => theme.Replace("<ItemsPresenter Name=\"PART_ItemsPresenter\" Margin=\"4\" />", """
            <DockPanel>
              <Border a:Web.ScrollButton="Up" DockPanel.Dock="Top" Padding="0,4" IsVisible="False"><Path Width="16" Height="16" Data="M4,10 L8,6 L12,10" Stroke="#0a0a0a" StrokeThickness="1.333" HorizontalAlignment="Center" /></Border>
              <Border a:Web.ScrollButton="Down" DockPanel.Dock="Bottom" Padding="0,4"><Path Width="16" Height="16" Data="M4,6 L8,10 L12,6" Stroke="#0a0a0a" StrokeThickness="1.333" HorizontalAlignment="Center" /></Border>
              <ScrollViewer VerticalScrollBarVisibility="Hidden"><ItemsPresenter Name="PART_ItemsPresenter" Margin="4" /></ScrollViewer>
            </DockPanel>
            """, StringComparison.Ordinal).Replace("<Border Name=\"PopupBorder\" ", "<Border Name=\"PopupBorder\" MaxHeight=\"160\" ", StringComparison.Ordinal);

    private Task<CompiledPage> ScrollingAsync() => fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + Scrolling(Theme) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" SelectedIndex=\"0\" Width=\"200\">"
        + string.Concat(Enumerable.Range(0, 12).Select(index => $"<ComboBoxItem Content=\"Item {index}\" />")) + "</ComboBox></StackPanel>");

    [Fact]
    [Trait("AC", "ITEMS-141")]
    public async Task AnOpeningListScrollsItsSelectedOptionTheNearestWayAsync()
    {
        var page = await fixture.CompileAsync("<StackPanel><StackPanel.Resources>" + Scrolling(Theme) + "</StackPanel.Resources><ComboBox Theme=\"{StaticResource Select}\" SelectedIndex=\"8\" Width=\"200\">"
            + string.Concat(Enumerable.Range(0, 12).Select(index => $"<ComboBoxItem Content=\"Item {index}\" />")) + "</ComboBox></StackPanel>");
        const string Shown = "() => getComputedStyle(document.querySelector('[data-aioros-scroller=up]')).display === 'block'";

        var found = await BrowserLayout.ClockedAsync(page, 600, 500, [Aioros.Targets.Ssr.Documents.ScrollerScript.Bytes], async tab =>
        {
            await tab.ClickAsync("select");
            var up = false;
            for (var attempt = 0; attempt < 2000 && !up; attempt++)
            {
                up = await tab.EvaluateAsync<bool>(Shown);
            }

            return $"{up} " + await tab.EvaluateAsync<int>("() => Math.round(document.querySelector('[data-aioros-scroller=down]').getBoundingClientRect().top - document.querySelector('option:checked').getBoundingClientRect().bottom)");
        });

        Assert.Equal("True 0", found);
    }

    [Fact]
    [Trait("AC", "ITEMS-134")]
    public async Task ScrollButtonsStandAtTheListsEdgesWhileItCanScrollAsync()
    {
        var page = await ScrollingAsync();
        var plain = await fixture.CompileAsync(Page("""<ComboBox Theme="{StaticResource Select}" SelectedIndex="0"><ComboBoxItem Content="Apple" /></ComboBox>"""));
        var shown = await BrowserLayout.ClickAndEvaluateAsync(page, 600, 500, "select", "() => getComputedStyle(document.querySelector('[data-aioros-scroller=down]')).display === 'block'", "() => [...document.querySelectorAll('[data-aioros-scroller]')].map(b => b.dataset.aiorosScroller + ':' + getComputedStyle(b).display + ':' + Math.round(b.getBoundingClientRect().height)).join(' ') + ' ' + Math.round(document.querySelector('[data-aioros-scroller=down]').getBoundingClientRect().width - document.querySelector('option').getBoundingClientRect().width) + ' ' + getComputedStyle(document.querySelector('[data-aioros-scroller=up]')).height");

        var html = EngineFixture.Body(page);
        Assert.Matches("<select [^>]*><div aria-hidden=\"true\" data-aioros-scroller=\"up\"></div><option", html);
        Assert.Matches("</option><div aria-hidden=\"true\" data-aioros-scroller=\"down\"></div></select>", html);
        Assert.Matches("::picker\\(select\\)\\{container-type:scroll-state;container-name:aioros-pick;scroll-padding-block:24px\\}", page.Css);
        Assert.Matches("@container aioros-pick scroll-state\\(scrollable:top\\)\\{[^}]*\\[data-aioros-scroller=up\\]\\{display:block\\}\\}", page.Css);
        Assert.Equal("up:none:0 down:block:24 8 24px", shown);
        Assert.DoesNotContain("data-aioros-scroller", EngineFixture.Body(plain), StringComparison.Ordinal);
    }

    private static string Page(string body) => $"""
        <StackPanel>
          <StackPanel.Resources>{Theme}</StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    private static string Rules(CompiledPage page)
    {
        var classes = Regex.Match(EngineFixture.Body(page), "<select[^>]* class=\"([^\"]+)\"").Groups[1].Value.Split(' ');
        return string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal))));
    }
}
