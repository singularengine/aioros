using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Avalonia.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class AvaloniaPageEngineTests(EngineFixture fixture)
{
    private static readonly string[] ThreeNames = ["a1", "b2", "c3"];

    [Fact]
    [Trait("AC", "ENGINE-01")]
    public async Task SimpleBindingsCompileToReusablePlansAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("Simple"), "light", new Article("First", "One"));

        var body = EngineFixture.Body(page, new Article("Second", "Two"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(">Second</h1>", body, StringComparison.Ordinal);
        Assert.Contains(">Two</div>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("First", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-02")]
    public async Task UnplannableBindingsFallBackToTheLiteralRenderAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("Faded"), "light", new Article("Faded title"));

        Assert.False(page.Reusable);
        Assert.Contains("SkewTransform.AngleX", page.Failure, StringComparison.Ordinal);
        Assert.Contains("Faded title", EngineFixture.Body(page, new Article("ignored")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-03")]
    public async Task BoundVisibilityBecomesAConditionAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("Visible"), "light", new Secret(show: true));
        var negated = await fixture.Engine.RenderXamlAsync(
            """
            <StackPanel xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:m="using:Aioros.Avalonia.Tests.Models" x:DataType="m:Secret">
              <TextBlock Text="when-on" IsVisible="{Binding Show}" />
              <TextBlock Text="when-off" IsVisible="{Binding !Show}" />
            </StackPanel>
            """,
            [new Secret(show: true)],
            "light",
            TestContext.Current.CancellationToken);

        Assert.True(negated.Reusable, negated.Failure);
        Assert.DoesNotContain("when-off", EngineFixture.Body(negated, new Secret(show: true)), StringComparison.Ordinal);
        Assert.Contains("when-off", EngineFixture.Body(negated, new Secret(show: false)), StringComparison.Ordinal);
        Assert.DoesNotContain("when-on", EngineFixture.Body(negated, new Secret(show: false)), StringComparison.Ordinal);
        Assert.True(page.Reusable, page.Failure);
        Assert.DoesNotContain("secret", EngineFixture.Body(page, new Secret(show: false)), StringComparison.Ordinal);
        Assert.Contains("secret", EngineFixture.Body(page, new Secret(show: true)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-04")]
    public async Task BoundItemsBecomeARepeatAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("List"), "light", new Catalog(new Item("only")));

        var body = EngineFixture.Body(page, new Catalog(new Item("a1"), new Item("b2"), new Item("c3")));

        Assert.True(page.Reusable, page.Failure);
        Assert.All(ThreeNames, name => Assert.Contains($">{name}<", body, StringComparison.Ordinal));
        Assert.DoesNotContain("only", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-05")]
    public async Task DarkThemeColorsComeFromAvaloniaAsync()
    {
        var light = await fixture.RenderAsync(EngineFixture.Page("Simple"), "light", new Article("Theme"));
        var dark = await fixture.RenderAsync(EngineFixture.Page("Simple"), "dark", new Article("Theme"));

        Assert.Contains("color-scheme:dark", dark.Css, StringComparison.Ordinal);
        Assert.Contains("color-scheme:light", light.Css, StringComparison.Ordinal);
        Assert.NotEqual(light.Foreground, dark.Foreground);
    }

    [Fact]
    [Trait("AC", "ENGINE-06")]
    public async Task PagesRenderInsideTheirLayoutAsync()
    {
        var definition = EngineFixture.Page("Shell", "Pages/Shell/Layout.axaml");
        var page = await fixture.RenderAsync(definition, "light", new Brand("Lumen"), new Article("Inbox"));

        var body = EngineFixture.Body(page, new Brand("Acme"), new Article("Issues"));

        Assert.True(page.Reusable, page.Failure);
        Assert.True(body.IndexOf(">Acme<", StringComparison.Ordinal) is >= 0 and var brand && brand < body.IndexOf(">Issues<", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "ENGINE-07")]
    public async Task PostedFieldsReachTheModelAndRunTheCommandAsync()
    {
        var signup = new Signup();

        var outcome = await fixture.PostAsync(EngineFixture.Page("Form"), signup, ("f0", "Ada"), ("_btn", "0|current"));

        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.Equal("Ada", signup.Name);
        Assert.Equal(1, signup.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-08")]
    public async Task StaleButtonsConflictAsync()
    {
        var signup = new Signup();

        var outcome = await fixture.PostAsync(EngineFixture.Page("Form"), signup, ("f0", "Ada"), ("_btn", "0|stale"));

        Assert.Equal(PostStatus.Conflict, outcome.Status);
        Assert.Equal(0, signup.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-09")]
    public async Task ReloadedXamlIsUsedAndBrokenXamlIsRejectedAsync()
    {
        var definition = EngineFixture.Page("Reload");
        const string Changed = """
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:m="using:Aioros.Avalonia.Tests.Models" x:DataType="m:Article">
              <TextBlock Text="{Binding Title, StringFormat='Hot {0}'}" />
            </UserControl>
            """;

        var accepted = await fixture.Engine.ReloadAsync(definition.Source, Changed, TestContext.Current.CancellationToken);
        var rejected = await fixture.Engine.ReloadAsync(definition.Source, "<UserControl", TestContext.Current.CancellationToken);
        var page = await fixture.RenderAsync(definition, "light", new Article("Reload"));

        Assert.Null(accepted);
        Assert.False(string.IsNullOrEmpty(rejected));
        Assert.Contains("Hot Reload", EngineFixture.Body(page, new Article("Reload")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-10")]
    public async Task LiteralValuesAreEncodedAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("Faded"), "light", new Article("Tom & <Jerry>"));

        Assert.Contains("Tom &amp; &lt;Jerry&gt;", EngineFixture.Body(page), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-11")]
    public async Task XamlFragmentsRenderLikePagesAsync()
    {
        var xaml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Pages", "Simple", "Page.axaml"), TestContext.Current.CancellationToken);
        var model = new Article("Same", "Output");

        var fragment = await fixture.Engine.RenderXamlAsync(xaml, [model], "light", TestContext.Current.CancellationToken);
        var page = await fixture.RenderAsync(EngineFixture.Page("Simple"), "light", model);

        Assert.Equal(EngineFixture.Body(page, model), EngineFixture.Body(fragment, model));
        Assert.Equal(page.Css, fragment.Css);
    }

    [Fact]
    [Trait("AC", "ENGINE-12")]
    public async Task PagesAreCapturedAsPngAsync()
    {
        var png = (await fixture.Engine.CaptureAsync(EngineFixture.Page("Simple"), [new Article("Captured")], new CaptureOptions("light", 320, 200), TestContext.Current.CancellationToken)).Png;

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
        Assert.Equal(320, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(200, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Fact]
    [Trait("AC", "ENGINE-14")]
    public async Task XamlFragmentsAcceptPostsAsync()
    {
        var signup = new Signup();
        const string Form = """
            <StackPanel>
              <TextBox Text="{Binding Name}" />
              <Button Content="Save" Command="{Binding SaveCommand}" CommandParameter="{Binding Token}" />
            </StackPanel>
            """;

        var outcome = await Controls.ControlHarness.PostAsync(fixture, Form, signup, ("f0", "Grace"), ("_btn", "0|current"));

        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.Equal("Grace", signup.Name);
        Assert.Equal(1, signup.Saved);
    }

    [Fact]
    [Trait("AC", "ENGINE-15")]
    public async Task UiWorkRunsOnTheUiThreadAsync()
    {
        var caller = Environment.CurrentManagedThreadId;
        var onUiThread = false;
        var uiThread = 0;

        await ((Aioros.Pages.IUiThread)fixture.Engine).InvokeAsync(
            () =>
            {
                onUiThread = global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
                uiThread = Environment.CurrentManagedThreadId;
            },
            TestContext.Current.CancellationToken);

        Assert.True(onUiThread);
        Assert.NotEqual(uiThread, Environment.CurrentManagedThreadId);
        Assert.NotEqual(0, caller);
    }

    [Fact]
    [Trait("AC", "ENGINE-16")]
    public async Task ApplicationStylesReloadAsync()
    {
        static string App(string color) => $$"""
            <Application xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="Aioros.Avalonia.Tests.TestApp">
              <Application.Styles>
                <FluentTheme />
                <StyleInclude Source="avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" />
                <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />
                <Style Selector="TextBlock.brand"><Setter Property="Foreground" Value="{{color}}" /></Style>
              </Application.Styles>
            </Application>
            """;
        const string Body = """<TextBlock Classes="brand" Text="styled" />""";

        var first = await fixture.Engine.ReloadAsync("App.axaml", App("#ff0000"), TestContext.Current.CancellationToken);
        var red = await Controls.ControlHarness.CompileAsync(fixture, Body);
        var second = await fixture.Engine.ReloadAsync("App.axaml", App("#0000ff"), TestContext.Current.CancellationToken);
        var blue = await Controls.ControlHarness.CompileAsync(fixture, Body);
        var broken = await fixture.Engine.ReloadAsync("App.axaml", "<Application", TestContext.Current.CancellationToken);
        var kept = await Controls.ControlHarness.CompileAsync(fixture, Body);
        await fixture.Engine.ReloadAsync("App.axaml", App("#111111").Replace("""<Style Selector="TextBlock.brand"><Setter Property="Foreground" Value="#111111" /></Style>""", string.Empty, StringComparison.Ordinal), TestContext.Current.CancellationToken);

        Assert.Null(first);
        Assert.Null(second);
        Assert.False(string.IsNullOrEmpty(broken));
        Assert.Contains("color:#ff0000", red.Css, StringComparison.Ordinal);
        Assert.Contains("color:#0000ff", blue.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("color:#ff0000", blue.Css, StringComparison.Ordinal);
        Assert.Contains("color:#0000ff", kept.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-17")]
    public async Task TearingDownKeepsPostedValuesAsync()
    {
        var paging = new Paging();

        await Controls.ControlHarness.PostAsync(fixture, """<PipsPager NumberOfPages="{Binding Count}" SelectedPageIndex="{Binding Page}" />""", paging, ("f0", "2"));

        Assert.Equal(2, paging.Page);
    }

    [Fact]
    [Trait("AC", "ENGINE-18")]
    public async Task ControlThemesHaveTheirOwnLooksAsync()
    {
        var page = await Controls.ControlHarness.CompileAsync(fixture, """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Pink" TargetType="Button">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <ContentPresenter Name="PART_ContentPresenter" Background="#ff00aa" Content="{TemplateBinding Content}" />
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Button Content="Fluent" />
              <Button Theme="{StaticResource Pink}" Content="Pink" />
            </StackPanel>
            """);

        Assert.Contains("background:#ff00aa", page.Css, StringComparison.Ordinal);
        Assert.Matches("background:(?!#ff00aa)", page.Css);
    }

    [Fact]
    [Trait("AC", "ENGINE-22")]
    public async Task ThePageIsMarkedInsideItsLayoutAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("Shell", "Pages/Shell/Layout.axaml"), "light", new Brand("Lumen"), new Article("Inbox"));

        var body = EngineFixture.Body(page, new Brand("Acme"), new Article("Issues"));
        var open = body.IndexOf("<div data-aioros-page style=\"display:contents\"><!--p-->", StringComparison.Ordinal);
        var close = body.IndexOf("<!--/p--></div>", StringComparison.Ordinal);

        Assert.True(page.Reusable, page.Failure);
        Assert.True(open >= 0 && close > open, body);
        Assert.InRange(body.IndexOf(">Issues<", StringComparison.Ordinal), open, close);
        Assert.True(body.IndexOf(">Acme<", StringComparison.Ordinal) < open);
    }

    [Fact]
    [Trait("AC", "ENGINE-23")]
    public async Task BindingsThePlanCannotReproduceAreNotFrozenAsync()
    {
        var multi = await fixture.CompileAsync("""<StackPanel><StackPanel.Resources><m:Corners x:Key="C" /></StackPanel.Resources><Border><Border.CornerRadius><MultiBinding Converter="{StaticResource C}"><Binding Path="Show" /><Binding Path="Show" /></MultiBinding></Border.CornerRadius></Border></StackPanel>""", new Secret(true));
        var converted = await fixture.CompileAsync("""<StackPanel><StackPanel.Resources><m:Tuned x:Key="C" Suffix="!" /></StackPanel.Resources><Border Tag="{Binding Show, Converter={StaticResource C}}" /></StackPanel>""", new Secret(true));

        Assert.False(multi.Reusable);
        Assert.Contains("Border.CornerRadius", multi.Failure, StringComparison.Ordinal);
        Assert.Contains("MultiBinding", multi.Failure, StringComparison.Ordinal);
        Assert.False(converted.Reusable);
        Assert.Contains("Border.Tag", converted.Failure, StringComparison.Ordinal);
        Assert.Contains("converter", converted.Failure, StringComparison.Ordinal);
        foreach (var (xaml, property) in new[]
        {
            ("""<Border Height="4" BorderThickness="{Binding Gap}" />""", "Border.BorderThickness"),
            ("""<TextBlock Text="x" Tag="{Binding Mode}" />""", "TextBlock.Tag"),
            ("""<Border Height="4" Background="Red"><Border.RenderTransform><TransformGroup><SkewTransform AngleX="{Binding Size2}" /></TransformGroup></Border.RenderTransform></Border>""", "SkewTransform.AngleX"),
        })
        {
            var page = await fixture.CompileAsync(xaml, Tile.On);
            Assert.False(page.Reusable, property);
            Assert.Contains(property, page.Failure, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-29")]
    public async Task BoundDataContextsScopeTheirControlsAsync()
    {
        const string Xaml = """<StackPanel><TextBlock Text="{Binding Text}" /><StackPanel DataContext="{Binding Child}"><TextBlock Text="{Binding Radius, StringFormat='r {0}'}" /></StackPanel></StackPanel>""";
        var other = new Tile(false, "other", 0, [], "#000000", "off", new Circle(7), 0, 0, "Left");
        var planned = await fixture.CompileAsync(Xaml, Tile.On);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, other), other);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(expected, EngineFixture.Body(planned, other));
        Assert.Equal(expected, EngineFixture.Body(saved, other));
        Assert.Contains(">r 7<", expected, StringComparison.Ordinal);
        Assert.Contains(">other<", expected, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-30")]
    public async Task AncestorBindingsReadTheirScopesAsync()
    {
        foreach (var (text, act) in new[]
        {
            ("{ReflectionBinding $parent[ItemsControl].DataContext.Text}", "{ReflectionBinding $parent[ItemsControl;0].DataContext.Act}"),
            ("{ReflectionBinding #list.DataContext.Text}", "{ReflectionBinding #list.DataContext.Act}"),
            ("{ReflectionBinding DataContext.Text, RelativeSource={RelativeSource AncestorType=ItemsControl}}", "{ReflectionBinding DataContext.Act, RelativeSource={RelativeSource AncestorType=ItemsControl}}"),
            ("{Binding $parent[ItemsControl].((m:Tile)DataContext).Text}", "{Binding $parent[ItemsControl].((m:Tile)DataContext).Act}"),
        })
        {
            var xaml = $$"""
                <ItemsControl x:Name="list" ItemsSource="{Binding Items}">
                  <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="x:String">
                      <StackPanel>
                        <TextBlock Text="{{text}}" />
                        <Button Command="{{act}}" CommandParameter="{Binding}" Content="{Binding}" />
                      </StackPanel>
                    </DataTemplate>
                  </ItemsControl.ItemTemplate>
                </ItemsControl>
                """;
            foreach (var (from, to) in new[] { (Tile.On, Tile.Off), (Tile.Off, Tile.On) })
            {
                var planned = await fixture.CompileAsync(xaml, from);
                var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
                var expected = EngineFixture.Body(await fixture.CompileAsync(xaml, to), to);

                Assert.True(planned.Reusable, planned.Failure);
                Assert.Equal(expected, EngineFixture.Body(planned, to));
                Assert.Equal(expected, EngineFixture.Body(saved, to));
            }

            var model = new Tile(true, "alpha", 0, ["one", "two"], "#000000", "on", new Circle(1), 0, 0, "Left");
            var page = await fixture.CompileAsync(xaml, model);
            var fields = PlanForms.Fields(page.Plan, [model]);
            var two = fields.Fields.Single(field => Equals(field.Scope, "two")).Id;
            var status = await PlanForms.ApplyAsync(page.Plan, [model], ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, PlanForms.ButtonValue(two, "two")), new Plans.AdapterSlotResolver());

            Assert.Equal(PostStatus.Rendered, status);
            Assert.Equal(["two"], model.Picked);
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-31")]
    public async Task BoundValuesWithFewPossibilitiesRenderEachAsync()
    {
        var models = new[]
        {
            Tile.On,
            Tile.Off,
            new Tile(true, "far", 0, [], "#000000", "on", new Circle(1), 0, 9, "nonsense"),
        };
        foreach (var xaml in new[]
        {
            """<TextBlock Text="x" HorizontalAlignment="{Binding Align}" />""",
            """<Grid ColumnDefinitions="*,*,*"><TextBlock Text="x" Grid.Column="{Binding Col}" /></Grid>""",
            """<StackPanel><StackPanel.Styles><Style Selector="TextBlock"><Setter Property="FontStyle" Value="Normal" /></Style></StackPanel.Styles><TextBlock Text="x" HorizontalAlignment="Center" TextAlignment="{Binding Align}" /></StackPanel>""",
        })
        {
            var planned = await fixture.CompileAsync(xaml, Tile.On);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            Assert.True(planned.Reusable, planned.Failure);
            foreach (var model in models)
            {
                var expected = EngineFixture.Body(await fixture.CompileAsync(xaml, model), model);
                Assert.Equal(expected, EngineFixture.Body(planned, model));
                Assert.Equal(expected, EngineFixture.Body(saved, model));
            }
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-32")]
    public async Task BoundStyleValuesAreInlineStylesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <Border Opacity="{Binding Size}" Height="4" Background="Red" Margin="{Binding Gap}" BoxShadow="{Binding Shadow}" />
              <TextBlock Text="x" FontSize="{Binding Size2}" />
              <Border Height="4" Background="Red"><Border.RenderTransform><RotateTransform Angle="{Binding Size2}" /></Border.RenderTransform></Border>
              <Border Height="4" Background="Red"><Border.RenderTransform><ScaleTransform ScaleX="{Binding Size}" ScaleY="2" /></Border.RenderTransform></Border>
              <Border Height="4" Background="Red"><Border.RenderTransform><TranslateTransform X="{Binding Size2}" /></Border.RenderTransform></Border>
            </StackPanel>
            """;
        foreach (var (from, to) in new[] { (Tile.On, Tile.Off), (Tile.Off, Tile.On) })
        {
            var planned = await fixture.CompileAsync(Xaml, from);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, to), to);

            Assert.True(planned.Reusable, planned.Failure);
            Assert.Equal(expected, EngineFixture.Body(planned, to));
            Assert.Equal(expected, EngineFixture.Body(saved, to));
            Assert.Contains($"opacity:{to.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)};", expected, StringComparison.Ordinal);
        }

        var list = await fixture.CompileAsync("""<ListBox Opacity="{Binding Size}"><ListBoxItem>Go</ListBoxItem></ListBox>""", Tile.On);
        Assert.False(list.Reusable);
        Assert.Contains("ListBox.Opacity", list.Failure, StringComparison.Ordinal);
        Assert.Contains("opacity:0.5", list.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-33")]
    public async Task BoundFlagsConvertAsAvaloniaDoesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles><Style Selector="TextBlock.off"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles>
              <TextBlock Text="empty" IsVisible="{Binding !Items.Length}" />
              <TextBlock Text="column" IsVisible="{Binding Col}" />
              <TextBlock Text="mode" IsVisible="{Binding Mode}" />
              <TextBlock Text="missing" IsVisible="{Binding !Items.Count}" />
              <TextBlock Text="first" Classes.off="{Binding !Col}" />
            </StackPanel>
            """;
        var models = new[] { Tile.On, Tile.Off, new Tile(false, "none", 0, [], "#000000", "true", new Circle(1), 0, 9, "Left") };
        var planned = await fixture.CompileAsync(Xaml, Tile.Off);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in models)
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-34")]
    public async Task ElementBindingsReadTheBoundPathAsync()
    {
        const string Xaml = """
            <Border Tag="{Binding Text}">
              <StackPanel>
                <TextBox x:Name="box" Text="{Binding Mode}" />
                <TextBlock Text="{Binding #box.Text}" />
                <TextBlock Text="{Binding $parent[Border].Tag}" />
                <CheckBox x:Name="flag" IsChecked="{Binding Flag}" />
                <TextBlock Text="shown when off" IsVisible="{Binding !#flag.IsChecked}" />
              </StackPanel>
            </Border>
            """;
        var planned = await fixture.CompileAsync(Xaml, Tile.On);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, Tile.Off), Tile.Off);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(expected, EngineFixture.Body(planned, Tile.Off));
        Assert.Equal(expected, EngineFixture.Body(saved, Tile.Off));
        Assert.Contains(">beta<", expected, StringComparison.Ordinal);
        Assert.Contains(">off<", expected, StringComparison.Ordinal);
        Assert.Contains(">shown when off<", expected, StringComparison.Ordinal);
        Assert.DoesNotContain(">shown when off<", EngineFixture.Body(planned, Tile.On), StringComparison.Ordinal);

        var labeled = await fixture.CompileAsync("""<StackPanel><Label Content="_Name" Target="{Binding #name}" /><TextBox x:Name="name" a:Web.Id="name" Text="{Binding Text}" /></StackPanel>""", Tile.On);
        Assert.True(labeled.Reusable, labeled.Failure);
        Assert.Contains("for=\"name\"", EngineFixture.Body(labeled, Tile.Off), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-35")]
    public async Task FieldsWithSeveralElementsShareTheirIdAsync()
    {
        const string Xaml = """
            <StackPanel>
              <Button Command="{Binding RepeatCommand}">Again</Button>
              <Button Command="{Binding SendCommand}">Send</Button>
              <CheckBox IsThreeState="True" IsChecked="{Binding Mixed}">Mixed</CheckBox>
              <StackPanel>
                <RadioButton IsChecked="{Binding Bold}">Bold</RadioButton>
                <RadioButton IsChecked="{Binding Italic}">Italic</RadioButton>
              </StackPanel>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, new Toggles());
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var other = new Toggles { Bold = false, Italic = true, Mixed = false };
        var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, other), other);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(expected, EngineFixture.Body(planned, other));
        Assert.Equal(expected, EngineFixture.Body(saved, other));
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(expected, "type=\"radio\" name=\"f0\""));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(expected, "type=\"radio\" name=\"f1\""));
    }

    [Fact]
    [Trait("AC", "ENGINE-36")]
    public async Task BoundPaddingAndCornerRadiusAreInlineStylesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <Border Height="4" Background="Red" Padding="{Binding Pad}" CornerRadius="{Binding Radius}" />
              <Border Height="4" Background="Red" Padding="{Binding Number}" CornerRadius="{Binding Text}" Margin="{Binding Text}" />
            </StackPanel>
            """;
        foreach (var (from, to) in new[] { (Shaped.On, Shaped.Off), (Shaped.Off, Shaped.On) })
        {
            var planned = await fixture.CompileAsync(Xaml, from);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, to), to);
            var literal = await fixture.CompileAsync($"""<Border Height="4" Background="Red" Padding="{to.Pad}" CornerRadius="{to.Radius}" />""");

            Assert.True(planned.Reusable, planned.Failure);
            Assert.Equal(expected, EngineFixture.Body(planned, to));
            Assert.Equal(expected, EngineFixture.Body(saved, to));
            Assert.Contains(Declaration(literal.Css, "padding") + Declaration(literal.Css, "border-radius").TrimEnd(';') + "\"", expected, StringComparison.Ordinal);
            Assert.Contains("margin:0px 0px 0px 0px;padding:0px 0px 0px 0px;border-radius:0px 0px 0px 0px\"", expected, StringComparison.Ordinal);
        }

        var list = await fixture.CompileAsync("""<ListBox Padding="{Binding Pad}"><ListBoxItem>Go</ListBoxItem></ListBox>""", Shaped.On);
        Assert.False(list.Reusable);
        Assert.Contains("ListBox.Padding", list.Failure, StringComparison.Ordinal);
        Assert.Contains("padding:2px 3px 4px 1px", list.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-37")]
    public async Task BoundSkewAndMatrixTransformsAreInlineStylesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <Border Height="4" Background="Red" RenderTransformOrigin="0,0"><Border.RenderTransform><SkewTransform AngleX="{Binding AngleX}" AngleY="{Binding AngleY}" /></Border.RenderTransform></Border>
              <Border Height="4" Background="Red"><Border.RenderTransform><MatrixTransform Matrix="{Binding Matrix}" /></Border.RenderTransform></Border>
              <TextBlock Text="x" RenderTransform="{Binding Transform}" />
              <Border Height="4" Background="Red" RenderTransform="{Binding Turn}" />
              <Border Height="4" Background="Red"><Border.RenderTransform><MatrixTransform Matrix="{Binding Text}" /></Border.RenderTransform></Border>
            </StackPanel>
            """;
        foreach (var (from, to) in new[] { (Shaped.On, Shaped.Off), (Shaped.Off, Shaped.On) })
        {
            var planned = await fixture.CompileAsync(Xaml, from);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, to), to);

            Assert.True(planned.Reusable, planned.Failure);
            Assert.Equal(expected, EngineFixture.Body(planned, to));
            Assert.Equal(expected, EngineFixture.Body(saved, to));
            Assert.Contains("transform-origin:", planned.Css, StringComparison.Ordinal);
        }

        var body = EngineFixture.Body(await fixture.CompileAsync(Xaml, Shaped.Off), Shaped.On);
        foreach (var transform in new[]
        {
            """<Border Height="4" Background="Red"><Border.RenderTransform><SkewTransform AngleX="12" AngleY="-20" /></Border.RenderTransform></Border>""",
            """<Border Height="4" Background="Red"><Border.RenderTransform><MatrixTransform Matrix="1,0.5,0,1,10,20" /></Border.RenderTransform></Border>""",
            """<Border Height="4" Background="Red" RenderTransform="rotate(30deg) translate(4px,2px)" />""",
            """<Border Height="4" Background="Red" RenderTransform="scale(2,3)" />""",
        })
        {
            var literal = await fixture.CompileAsync(transform);
            Assert.Contains(Declaration(literal.Css, "transform").TrimEnd(';') + "\"", body, StringComparison.Ordinal);
        }

        var identity = EngineFixture.Body(await fixture.CompileAsync(Xaml, Shaped.On), Shaped.Off);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(identity, "transform:matrix\\(1,0,0,1,0,0\\)\""));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(identity, "transform:none\""));

        foreach (var (xaml, property) in new[]
        {
            ("""<Border Height="4" Background="Red"><Border.RenderTransform><TransformGroup><SkewTransform AngleX="{Binding AngleX}" /></TransformGroup></Border.RenderTransform></Border>""", "SkewTransform.AngleX"),
            ("""<ListBox RenderTransform="{Binding Transform}"><ListBoxItem>Go</ListBoxItem></ListBox>""", "ListBox.RenderTransform"),
        })
        {
            var page = await fixture.CompileAsync(xaml, Shaped.On);
            Assert.False(page.Reusable, property);
            Assert.Contains(property, page.Failure, StringComparison.Ordinal);
            Assert.Contains("transform:matrix(", page.Css, StringComparison.Ordinal);
        }
    }

    private static string Declaration(string css, string property) =>
        System.Text.RegularExpressions.Regex.Match(css, $"[;{{]({property}:[^;]+;)").Groups[1].Value is { Length: > 0 } declaration ? declaration : $"no {property} in {css}";

    [Fact]
    [Trait("AC", "ENGINE-28")]
    public async Task EnabledStatesFollowTheModelAsync()
    {
        foreach (var xaml in new[]
        {
            """<Button IsEnabled="{Binding Flag}">Go</Button>""",
            """<Button Command="{Binding Act}" CommandParameter="{Binding Text}">Go</Button>""",
            """<StackPanel><StackPanel.Styles><Style Selector="TextBlock:disabled"><Setter Property="Foreground" Value="Red" /></Style></StackPanel.Styles><StackPanel IsEnabled="{Binding Flag}"><TextBox Text="{Binding Text}" /><TextBlock Text="x" /></StackPanel></StackPanel>""",
        })
        {
            foreach (var (from, to) in new[] { (Tile.On, Tile.Off), (Tile.Off, Tile.On) })
            {
                var planned = await fixture.CompileAsync(xaml, from);
                var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
                var expected = EngineFixture.Body(await fixture.CompileAsync(xaml, to), to);

                Assert.True(planned.Reusable, planned.Failure);
                Assert.Equal(expected, EngineFixture.Body(planned, to));
                Assert.Equal(expected, EngineFixture.Body(saved, to));
                Assert.Equal(to.Flag, !expected.Contains(" disabled", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-24")]
    public async Task BoundClassesRenderAsAvaloniaDoesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border"><Setter Property="Transitions"><Transitions><BrushTransition Property="Background" Duration="0:0:1" /></Transitions></Setter></Style>
                <Style Selector="Border.on"><Setter Property="Background" Value="#123456" /><Setter Property="Padding" Value="12" /></Style>
                <Style Selector="Border.on TextBlock"><Setter Property="FontWeight" Value="Bold" /></Style>
              </StackPanel.Styles>
              <Border Classes.on="{Binding Show}"><TextBlock Text="Card" /></Border>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, new Secret(true));
        var on = await fixture.CompileAsync(Xaml, new Secret(true));
        var off = await fixture.CompileAsync(Xaml, new Secret(false));
        var expectedOn = EngineFixture.Body(on, new Secret(true));
        var expectedOff = EngineFixture.Body(off, new Secret(false));

        Assert.True(planned.Reusable, planned.Failure);
        Assert.NotEqual(expectedOn, expectedOff);
        Assert.Equal(expectedOn, EngineFixture.Body(planned, new Secret(true)));
        Assert.Equal(expectedOff, EngineFixture.Body(planned, new Secret(false)));
        Assert.Empty(off.Css.Split('\n').Except(planned.Css.Split('\n')));

        const string Negated = """<StackPanel><StackPanel.Styles><Style Selector="TextBlock.off"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles><TextBlock Text="x" Classes.off="{Binding !Show}" /></StackPanel>""";
        var negated = await fixture.CompileAsync(Negated, new Secret(true));
        Assert.True(negated.Reusable, negated.Failure);
        Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Negated, new Secret(false)), new Secret(false)), EngineFixture.Body(negated, new Secret(false)));
    }

    [Fact]
    [Trait("AC", "ENGINE-25")]
    public async Task ConvertersRunAsAvaloniaRunsThemAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Resources>
                <m:PriorityBrush x:Key="Priority" />
                <m:Shout x:Key="Shout" />
              </StackPanel.Resources>
              <StackPanel.Styles>
                <Style Selector="Border.owned"><Setter Property="Padding" Value="9" /></Style>
              </StackPanel.Styles>
              <TextBlock Text="{Binding Name, Converter={StaticResource Shout}, ConverterParameter='!'}" />
              <TextBlock Text="{Binding Name, Converter={x:Static m:TextConverters.Whisper}}" />
              <TextBlock Text="Urgent" IsVisible="{Binding Priority, Converter={x:Static m:LevelMatch.Instance}, ConverterParameter={x:Static m:Level.High}}" />
              <TextBlock Text="Unowned" IsVisible="{Binding Owner, Converter={x:Static ObjectConverters.IsNull}}" />
              <TextBlock Text="Named" IsVisible="{Binding Name, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
              <Border Background="{Binding Priority, Converter={StaticResource Priority}}" Classes.owned="{Binding Owner, Converter={x:Static ObjectConverters.IsNotNull}}">
                <TextBlock Text="Card" />
              </Border>
            </StackPanel>
            """;
        var urgent = new Ticket("fix login", 3, "ana");
        var calm = new Ticket(string.Empty, 1, null);
        var planned = await fixture.CompileAsync(Xaml, urgent);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var tuned = await fixture.CompileAsync("""<StackPanel><StackPanel.Resources><m:Tuned x:Key="T" Suffix="?" /></StackPanel.Resources><TextBlock Text="{Binding Name, Converter={StaticResource T}}" /></StackPanel>""", urgent);
        var write = PlansFile.Write([new PlannedPage("/tuned", "light", tuned)]);

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var ticket in new[] { urgent, calm })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, ticket), ticket);
            Assert.Equal(expected, EngineFixture.Body(planned, ticket));
            Assert.Equal(expected, EngineFixture.Body(saved, ticket));
        }

        Assert.Contains(">FIX LOGIN!<", EngineFixture.Body(planned, urgent), StringComparison.Ordinal);
        Assert.Contains(">fix login...<", EngineFixture.Body(saved, urgent), StringComparison.Ordinal);
        Assert.Contains(">Urgent<", EngineFixture.Body(saved, urgent), StringComparison.Ordinal);
        Assert.DoesNotContain(">Urgent<", EngineFixture.Body(saved, calm), StringComparison.Ordinal);
        Assert.True(tuned.Reusable, tuned.Failure);
        Assert.Single(write.Skipped);
    }

    [Fact]
    [Trait("AC", "ENGINE-26")]
    public async Task ItemsTakeTheTemplateOfTheirTypeAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.DataTemplates>
                <DataTemplate x:DataType="m:Square"><Border Background="#eeeeee" Padding="4"><TextBlock Text="{Binding Label}" FontWeight="Bold" /></Border></DataTemplate>
              </StackPanel.DataTemplates>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.DataTemplates>
                  <DataTemplate x:DataType="m:Circle"><TextBlock Text="{Binding Radius, StringFormat='circle {0}'}" /></DataTemplate>
                  <DataTemplate x:DataType="m:Pin"><Button Command="{Binding Pick}" CommandParameter="{Binding Label}"><TextBlock Text="{Binding Label}" /></Button></DataTemplate>
                </ItemsControl.DataTemplates>
              </ItemsControl>
            </StackPanel>
            """;
        var tray = new Tray();
        var planned = await fixture.CompileAsync(Xaml, new Shapes(new Circle(1), new Pin("one", tray)));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var mixed = new Shapes(new Square(2, "two"), new Circle(3), new Pin("three", tray), new Square(4, "four"));
        var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, mixed), mixed);

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(expected, EngineFixture.Body(planned, mixed));
        Assert.Equal(expected, EngineFixture.Body(saved, mixed));
        Assert.Contains(">circle 3<", expected, StringComparison.Ordinal);
        Assert.Contains(">four<", expected, StringComparison.Ordinal);
        Assert.DoesNotContain(" disabled", expected, StringComparison.Ordinal);

        const string Content = """
            <StackPanel>
              <StackPanel.DataTemplates>
                <DataTemplate x:DataType="m:Circle"><TextBlock Text="{Binding Radius, StringFormat='circle {0}'}" /></DataTemplate>
                <DataTemplate x:DataType="m:Square"><TextBlock Text="{Binding Label}" FontWeight="Bold" /></DataTemplate>
              </StackPanel.DataTemplates>
              <ContentControl Content="{Binding Child}" Padding="4" />
              <ContentControl Content="{Binding Mode, StringFormat='mode: {0}'}" />
            </StackPanel>
            """;
        var content = await fixture.CompileAsync(Content, Tile.On);
        var contentSaved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", content)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        var square = EngineFixture.Body(await fixture.CompileAsync(Content, Tile.Off), Tile.Off);
        Assert.True(content.Reusable, content.Failure);
        Assert.Equal(square, EngineFixture.Body(content, Tile.Off));
        Assert.Equal(square, EngineFixture.Body(contentSaved, Tile.Off));
        Assert.Contains(">sq<", square, StringComparison.Ordinal);
        Assert.Contains(">mode: off<", square, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-27")]
    public async Task MultiBindingsRenderAsAvaloniaDoesAsync()
    {
        const string Xaml = """
            <StackPanel>
              <StackPanel.Resources><m:Ratio x:Key="Ratio" /></StackPanel.Resources>
              <TextBlock><TextBlock.Text><MultiBinding StringFormat="{}{0}/{1} done"><Binding Path="Done" /><Binding Path="Total" /></MultiBinding></TextBlock.Text></TextBlock>
              <TextBlock><TextBlock.Text><MultiBinding Converter="{StaticResource Ratio}"><Binding Path="Done" /><Binding Path="Total" /></MultiBinding></TextBlock.Text></TextBlock>
              <TextBlock Text="Yours and open">
                <TextBlock.IsVisible><MultiBinding Converter="{x:Static BoolConverters.And}"><Binding Path="Open" /><Binding Path="Mine" /></MultiBinding></TextBlock.IsVisible>
              </TextBlock>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Xaml, new Progress(1, 4, true, true));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var progress in new[] { new Progress(3, 4, true, false), new Progress(0, 0, false, false) })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Xaml, progress), progress);
            Assert.Equal(expected, EngineFixture.Body(planned, progress));
            Assert.Equal(expected, EngineFixture.Body(saved, progress));
        }

        Assert.Contains(">3/4 done<", EngineFixture.Body(planned, new Progress(3, 4, true, false)), StringComparison.Ordinal);
    }
}
