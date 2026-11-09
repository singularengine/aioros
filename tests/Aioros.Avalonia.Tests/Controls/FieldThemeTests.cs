using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class FieldThemeTests(EngineFixture fixture)
{
    private const string Theme = """
        <ControlTheme x:Key="Input" TargetType="TextBox">
          <Setter Property="Background" Value="Transparent" />
          <Setter Property="BorderBrush" Value="#e5e5e5" />
          <Setter Property="BorderThickness" Value="1" />
          <Setter Property="CornerRadius" Value="6" />
          <Setter Property="Padding" Value="12,4" />
          <Setter Property="Template">
            <ControlTemplate>
              <DataValidationErrors>
                <Panel>
                  <Border Name="PART_BorderElement" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                          BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}">
                    <Border.Transitions>
                      <Transitions>
                        <BrushTransition Property="BorderBrush" Duration="0:0:0.15" />
                        <BoxShadowsTransition Property="BoxShadow" Duration="0:0:0.15" />
                      </Transitions>
                    </Border.Transitions>
                  </Border>
                  <Border Margin="{TemplateBinding BorderThickness}">
                    <TextPresenter Name="PART_TextPresenter" Text="{TemplateBinding Text, Mode=TwoWay}" Margin="{TemplateBinding Padding}" />
                  </Border>
                </Panel>
              </DataValidationErrors>
            </ControlTemplate>
          </Setter>
          <Style Selector="^ /template/ Border#PART_BorderElement"><Setter Property="BoxShadow" Value="0 1 2 0 #0d000000" /></Style>
          <Style Selector="^:focus /template/ Border#PART_BorderElement">
            <Setter Property="BorderBrush" Value="#a1a1a1" />
            <Setter Property="BoxShadow" Value="0 0 0 3 #80a1a1a1" />
          </Style>
          <Style Selector="^:error /template/ Border#PART_BorderElement"><Setter Property="BorderBrush" Value="#e7000b" /></Style>
          <Style Selector="^:error:focus /template/ Border#PART_BorderElement"><Setter Property="BoxShadow" Value="0 0 0 3 #33e7000b" /></Style>
        </ControlTheme>
        <ControlTheme x:Key="{x:Type DataValidationErrors}" TargetType="DataValidationErrors">
          <Setter Property="Template">
            <ControlTemplate TargetType="DataValidationErrors">
              <DockPanel>
                <ContentControl Margin="0,12,0,0" Content="{Binding (DataValidationErrors.Errors)}" ContentTemplate="{TemplateBinding ErrorTemplate}"
                                DataContext="{TemplateBinding Owner}" DockPanel.Dock="Bottom" IsVisible="{Binding (DataValidationErrors.HasErrors)}" />
                <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
              </DockPanel>
            </ControlTemplate>
          </Setter>
          <Setter Property="ErrorTemplate">
            <DataTemplate>
              <ItemsControl ItemsSource="{Binding}" Foreground="#e7000b" FontSize="14">
                <ItemsControl.Styles><Style Selector="TextBlock"><Setter Property="LineHeight" Value="20" /></Style></ItemsControl.Styles>
              </ItemsControl>
            </DataTemplate>
          </Setter>
        </ControlTheme>
        """;

    [Fact]
    [Trait("AC", "TEXT-34")]
    public async Task FieldShadowsAreWrittenAtRestAndInTheirStatesAsync()
    {
        var page = await fixture.CompileAsync(Page("""<TextBox Theme="{StaticResource Input}" Text="a" />"""));

        var rules = Rules(page, "input");
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*box-shadow:0px 1px 2px 0px rgba\\(0,0,0,0\\.051\\);", rules);
        Assert.Matches(":focus\\{border-color:#a1a1a1!important;box-shadow:0px 0px 0px 3px rgba\\(161,161,161,0\\.502\\)!important;\\}", rules);
    }

    [Fact]
    [Trait("AC", "TEXT-35")]
    public async Task TransparentFieldBackgroundsAreWrittenAsync()
    {
        var page = await fixture.CompileAsync(Page("""<TextBox Theme="{StaticResource Input}" Text="a" /><TextBox Theme="{StaticResource Input}" AcceptsReturn="True" IsEnabled="False" Text="b" />"""));

        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*background:transparent;", Rules(page, "input"));
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*background:transparent;", Rules(page, "textarea"));
    }

    [Fact]
    [Trait("AC", "TEXT-36")]
    public async Task FieldStatesAreReadWithTheirTransitionsHeldAsync()
    {
        var page = await fixture.CompileAsync(Page("""<TextBox Theme="{StaticResource Input}" Text="a" />"""));

        var rules = Rules(page, "input");
        Assert.Matches("\\.k[0-9a-f]+\\{[^}]*border-color:#e5e5e5;", rules);
        Assert.Matches("transition:border-color 150ms linear 0ms,box-shadow 150ms linear 0ms;", rules);
        Assert.Contains(":focus{border-color:#a1a1a1!important;", rules, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-37")]
    public async Task InvalidFieldsAreMarkedAndDescribedAsync()
    {
        const string Body = """<TextBox Theme="{StaticResource Input}" Text="{Binding Name}" />""";
        var page = await fixture.PlansLikeAvaloniaAsync(Page(Body), new Registration(), Invalid());

        var html = EngineFixture.Body(page, Invalid());
        Assert.Matches("<input type=\"text\" name=\"f0\" aria-invalid=\"true\" aria-describedby=\"e0\" class=\"[^\"]+\"[^>]*><div id=\"e0\" class=\"vw\"><div role=\"alert\" class=\"ve[0-9a-f]+\">A name is required.</div></div>", html);
        Assert.Matches("<input type=\"text\" name=\"f0\" class=\"[^\"]+\"[^>]*><div id=\"e0\" class=\"vw\"></div>", EngineFixture.Body(page, new Registration()));
        var rules = Rules(page, "input");
        Assert.Contains("[aria-invalid=\"true\"]{border-color:#e7000b!important;}", rules, StringComparison.Ordinal);
        Assert.Contains(":focus[aria-invalid=\"true\"]{border-color:#e7000b!important;box-shadow:0px 0px 0px 3px rgba(231,0,11,0.2)!important;}", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("border-color:#c42b1c!important", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-38")]
    public async Task ValidationMessagesTakeTheirThemesLookAsync()
    {
        var page = await fixture.CompileAsync(Page("""<TextBox Theme="{StaticResource Input}" Text="{Binding Name}" />"""), new Registration());
        var fluent = await fixture.CompileAsync("""<TextBox Text="{Binding Name}" />""", new Registration());
        var spaced = await fixture.CompileAsync(Page("""<StackPanel Spacing="12"><TextBox Theme="{StaticResource Input}" Text="{Binding Name}" /></StackPanel>"""), new Registration());

        Assert.Matches("\\.ve[0-9a-f]+\\{font-size:14px;font-weight:400;color:#e7000b;line-height:20px;margin:12px 0px 0px 0px;overflow-wrap:anywhere\\}", page.Css);
        Assert.Matches("\\.ve[0-9a-f]+\\{font-size:14px;font-weight:400;color:#[0-9a-f]{6};margin:4px 0px 0px 0px;overflow-wrap:anywhere\\}", fluent.Css);
        Assert.Matches("\\.ve[0-9a-f]+\\{font-size:14px;font-weight:400;color:#e7000b;line-height:20px;margin:12px 0px 0px 0px;overflow-wrap:anywhere\\}", spaced.Css);
    }

    [Fact]
    [Trait("AC", "TEXT-46")]
    public async Task MessagesShareTheirFieldsGridCellAsync()
    {
        var body = Page($$"""
            <Grid RowDefinitions="Auto,12,Auto,12,Auto,*" Width="320" HorizontalAlignment="Left">
              <Grid.Styles>{{Messages}}</Grid.Styles>
              <TextBlock Text="Email" />
              <TextBox Grid.Row="2" Theme="{StaticResource Input}" Text="mail">{{Error("Enter a valid email address.")}}</TextBox>
              <TextBlock Grid.Row="4" Text="After" />
            </Grid>
            """);
        var page = await fixture.CompileAsync(body);
        var modeled = await fixture.CompileAsync(Page("""<Grid RowDefinitions="Auto,12,Auto,*"><TextBlock Text="Name" /><TextBox Grid.Row="2" Theme="{StaticResource Input}" Text="{Binding Name}" /></Grid>"""), new Registration());
        var (desktop, web) = await PlacesAsync(body, page);

        Assert.Matches("<div class=\"[^\"]+\"><input [^>]*aria-invalid=\"true\"[^>]*><div id=\"e0\" class=\"vw\"><div role=\"alert\"[^>]*>Enter a valid email address\\.</div></div></div>", EngineFixture.Body(page));
        Assert.Matches("<div class=\"[^\"]+\"><input [^>]*aria-invalid=\"true\"[^>]*><div id=\"e0\" class=\"vw\"><div role=\"alert\"[^>]*>A name is required\\.</div></div></div>", EngineFixture.Body(modeled, Invalid()));
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    [Fact]
    [Trait("AC", "TEXT-47")]
    public async Task AFieldsMarginAndWidthHoldItsMessagesAsync()
    {
        var body = Page($$"""
            <StackPanel Spacing="12">
              <StackPanel.Styles>{{Messages}}</StackPanel.Styles>
              <TextBlock Text="Email" />
              <TextBox Theme="{StaticResource Input}" Margin="24,8,16,20" Width="240" HorizontalAlignment="Left" Text="mail">{{Error("Enter a valid email address.")}}</TextBox>
              <TextBlock Text="After" />
            </StackPanel>
            """);
        var page = await fixture.CompileAsync(body);
        var (desktop, web) = await PlacesAsync(body, page);
        var width = await BrowserLayout.EvaluateAsync(page, 400, 300, "() => String(document.querySelector('[role=alert]').getBoundingClientRect().width)");

        Assert.DoesNotMatch("margin:|width:240px", Rules(page, "input"));
        Assert.Equal("240", width);
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    [Fact]
    [Trait("AC", "TEXT-48")]
    public async Task AFieldsHeightHoldsItsMessagesAsync()
    {
        var body = Page($$"""
            <StackPanel>
              <StackPanel.Styles>{{Messages}}</StackPanel.Styles>
              <TextBox Theme="{StaticResource Input}" MinHeight="120" Text="mail">{{Error("Required.")}}</TextBox>
              <TextBlock Text="After the minimum" />
              <TextBox Theme="{StaticResource Input}" Height="110" Text="mail">{{Error("Too short.")}}</TextBox>
              <TextBlock Text="After the height" />
              <TextBox MinHeight="60" Text="mail">{{Error("Fluent.")}}</TextBox>
              <TextBlock Text="After Fluent" />
            </StackPanel>
            """);
        var page = await fixture.CompileAsync(body);
        var (desktop, web) = await PlacesAsync(body, page, 600);

        Assert.DoesNotContain("min-height:120px", Rules(page, "input"), StringComparison.Ordinal);
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    [Fact]
    [Trait("AC", "TEXT-49")]
    public async Task AFieldsEffectsHoldItsMessagesAsync()
    {
        var body = Page($$"""
            <StackPanel>
              <StackPanel.Styles>{{Messages}}</StackPanel.Styles>
              <TextBox Theme="{StaticResource Input}" Opacity="0.5" RenderTransform="translate(10px,20px)" ZIndex="3" Text="mail">{{Error("Enter a valid email address.")}}</TextBox>
              <TextBlock Text="After" />
            </StackPanel>
            """);
        var page = await fixture.CompileAsync(body);
        var bound = await fixture.CompileAsync(Page("""<TextBox Theme="{StaticResource Input}" Opacity="{Binding Fade}" Text="{Binding Name}" />"""), new FadedRegistration());
        var (desktop, web) = await PlacesAsync(body, page);
        var opacity = await BrowserLayout.EvaluateAsync(page, 400, 300, "() => { let o = 1; for (let e = document.querySelector('[role=alert]'); e; e = e.parentElement) o *= Number(getComputedStyle(e).opacity); return String(o); }");

        Assert.DoesNotMatch("opacity:|transform:|z-index:", Rules(page, "input"));
        Assert.Equal("0.5", opacity);
        Assert.Matches("<div class=\"wb[^\"]*\" style=\"opacity:0\\.5\"><input (?![^>]*style=)[^>]*>", EngineFixture.Body(bound, FadedRegistration.Invalid()));
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    [Fact]
    [Trait("AC", "TEXT-50")]
    public async Task OtherFieldsHoldTheirMessagesAsync()
    {
        var body = Page($$"""
            <StackPanel>
              <StackPanel.Styles>{{Messages}}</StackPanel.Styles>
              <ComboBox MinHeight="120" HorizontalAlignment="Stretch">{{Error("Choose one.")}}</ComboBox>
              <TextBlock Text="After the list" />
              <NumericUpDown Height="110">{{Error("Too many.")}}</NumericUpDown>
              <TextBlock Text="After the number" />
              <CalendarDatePicker>{{Error("Pick a date.")}}</CalendarDatePicker>
              <TextBlock Text="After the date" />
            </StackPanel>
            """);
        var page = await fixture.CompileAsync(body);
        var (desktop, web) = await PlacesAsync(body, page, 600, new HashSet<string>(StringComparer.Ordinal) { "Choose one.", "After the list", "Too many.", "After the number", "Pick a date.", "After the date" });

        Assert.DoesNotMatch("height:120px", Rules(page, "select"));
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    [Fact]
    [Trait("AC", "TEXT-51")]
    public async Task MessagesShowOnlyWhereTheTemplatePlacesThemAsync()
    {
        var body = Page($$"""
            <StackPanel>
              <StackPanel.Styles>{{Messages}}</StackPanel.Styles>
              <Button Height="36" Content="Pick a date">{{Error("Pick a date.")}}</Button>
              <TextBlock Text="After" />
            </StackPanel>
            """);
        var page = await fixture.CompileAsync(body);
        var planned = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Bare" TargetType="TextBox">
                  <Setter Property="Template">
                    <ControlTemplate><Border Name="PART_BorderElement" BorderThickness="1" BorderBrush="#e5e5e5"><TextPresenter Name="PART_TextPresenter" Text="{TemplateBinding Text, Mode=TwoWay}" /></Border></ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <TextBox Theme="{StaticResource Bare}" Text="{Binding Name}" />
            </StackPanel>
            """, new Registration());
        var captured = await fixture.CaptureInAsync(body, new CaptureOptions("light", 400, 300));
        var (desktop, web) = await PlacesAsync(body, page, shown: new HashSet<string>(StringComparer.Ordinal) { "After" });
        var height = await BrowserLayout.EvaluateAsync(page, 400, 300, "() => String(document.querySelector('button').getBoundingClientRect().height)");
        var invalid = EngineFixture.Body(planned, Invalid());

        Assert.DoesNotContain(captured.Texts, text => text.Text == "Pick a date.");
        Assert.DoesNotMatch("role=\"alert\"|class=\"wb|Pick a date\\.", EngineFixture.Body(page));
        Assert.Equal("36", height);
        Assert.True(planned.Reusable, planned.Failure);
        Assert.Contains("aria-invalid=\"true\"", invalid, StringComparison.Ordinal);
        Assert.DoesNotMatch("aria-describedby|role=\"alert\"|class=\"wb", invalid);
        Assert.True(desktop.SequenceEqual(web), $"desktop {string.Join(", ", desktop)}; web {string.Join(", ", web)}");
    }

    private const string Messages = """
        <Style Selector="DataValidationErrors">
          <Setter Property="ErrorTemplate">
            <DataTemplate>
              <ItemsControl ItemsSource="{Binding}" Foreground="#e7000b" FontSize="14">
                <ItemsControl.ItemTemplate><DataTemplate x:DataType="sys:Exception"><TextBlock Text="{Binding Message}" LineHeight="20" TextWrapping="Wrap" /></DataTemplate></ItemsControl.ItemTemplate>
              </ItemsControl>
            </DataTemplate>
          </Setter>
        </Style>
        """;

    private static string Error(string message) =>
        $"<DataValidationErrors.Error><sys:Exception><x:Arguments><x:String>{message}</x:String></x:Arguments></sys:Exception></DataValidationErrors.Error>";

    private async Task<(string[] Desktop, string[] Web)> PlacesAsync(string body, CompiledPage page, int height = 300, HashSet<string>? shown = null)
    {
        var desktop = Places((await fixture.CaptureInAsync(body, new CaptureOptions("light", 400, height))).Texts.RemoveAll(text => text.Text == "mail" || (shown is not null && !shown.Contains(text.Text))));
        var web = (await BrowserLayout.EvaluateAsync(page, 400, height, Boxes)).Split('\n').Where(line => shown is null || shown.Contains(line[..line.LastIndexOf('@')])).ToArray();
        return (desktop, web);
    }

    private const string Boxes = """
        () => [...document.body.querySelectorAll('*')].filter(e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()))
          .map(e => [e.textContent.trim(), e.getBoundingClientRect()]).sort((a, b) => a[1].top - b[1].top || a[1].left - b[1].left)
          .map(([text, box]) => `${text}@${Math.round(box.left)},${Math.round(box.top)}`).join('\n')
        """;

    private static string[] Places(ImmutableArray<LaidOutText> texts) =>
        [.. texts.OrderBy(text => text.Y).ThenBy(text => text.X).Select(text => $"{text.Text}@{Math.Round(text.X)},{Math.Round(text.Y)}")];

    private static Registration Invalid() => Registration.Invalid();

    private static string Page(string body) => $"""
        <StackPanel>
          <StackPanel.Resources>{Theme}</StackPanel.Resources>
          {body}
        </StackPanel>
        """;

    private static string Rules(CompiledPage page, string tag)
    {
        var classes = Regex.Match(EngineFixture.Body(page), $"<{tag}[^>]* class=\"([^\"]+)\"").Groups[1].Value.Split(' ');
        return string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal) || rule.StartsWith($".{name}[", StringComparison.Ordinal))));
    }
}
