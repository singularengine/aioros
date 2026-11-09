using System.Net;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class NavigationControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "NAVIGATION-01")]
    public async Task SplitViewPanesAreDrawersAsync()
    {
        var page = await fixture.CompileAsync("""
            <SplitView a:Web.Id="side" a:Web.CollapseBelow="700" OpenPaneLength="200" IsPaneOpen="True" DisplayMode="Inline">
              <SplitView.Pane><TextBlock Text="Pane" /></SplitView.Pane>
              <TextBlock Text="Main" />
            </SplitView>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<aside popover id=\"side\" class=\"pv[0-9a-f]+\"><div[^>]*>Pane</div></aside><div class=\"c\" style=\"flex:1 1 0;min-width:0\"><div[^>]*>Main</div>", html);
        Assert.Contains("@media (max-width:700px)", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-02")]
    public async Task ExpandersAreDetailsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Expander Header="Open" IsExpanded="True"><TextBlock Text="Shown" /></Expander>
              <Expander Header="Closed"><TextBlock Text="Hidden" /></Expander>
            </StackPanel>
            """);
        var styles = (global::Avalonia.Styling.Styles)global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load("""
            <Styles xmlns="https://github.com/avaloniaui">
              <Style Selector="Border.code"><Setter Property="Background" Value="#FF0000" /></Style>
              <Style Selector="SelectableTextBlock.source"><Setter Property="Foreground" Value="#00FF00" /></Style>
            </Styles>
            """);
        await fixture.Engine.InvokeAsync(() => global::Avalonia.Application.Current!.Styles.Add(styles), TestContext.Current.CancellationToken);
        Runtime.Rendering.CompiledPage styled;
        try
        {
            styled = await fixture.CompileAsync("""
                <Expander Header="Closed"><Border Classes="code" Padding="4"><SelectableTextBlock Classes="source" Text="code" /></Border></Expander>
                """);
        }
        finally
        {
            await fixture.Engine.InvokeAsync(() => global::Avalonia.Application.Current!.Styles.Remove(styles), TestContext.Current.CancellationToken);
        }

        Assert.Matches("<details class=\"c[^\"]*\" open><summary[^>]*><svg class=\"chev\"[^>]*>.*?</svg>Open</summary><div class=\"c\"><div[^>]*>Shown</div></div></details>", html);
        Assert.Matches("<details class=\"c[^\"]*\"><summary[^>]*><svg class=\"chev\"[^>]*>.*?</svg>Closed</summary>", html);
        Assert.Contains("background:#ff0000", styled.Css, StringComparison.Ordinal);
        Assert.Contains("color:#00ff00", styled.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-03")]
    public async Task MenusAreToolbarsOfPopoverMenusAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Menu>
              <MenuItem Header="_File">
                <MenuItem Header="Docs" a:Web.Href="/docs" />
              </MenuItem>
              <MenuItem Header="Help" a:Web.Href="/help" />
            </Menu>
            """);

        Assert.Matches("^<menu role=\"menubar\" data-aioros-overlay class=\"mb[^\"]*\"><li role=\"none\"><button type=\"button\" role=\"menuitem\" aria-haspopup=\"menu\" class=\"mbi [^\"]+\" popovertarget=\"m1\" style=\"anchor-name:--m1\">File</button>"
            + "<div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+\" style=\"position-anchor:--m1;position-area:bottom span-right;position-try-fallbacks:flip-block;\"><a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/docs\"><span class=\"mh\">Docs</span></a></div></li>"
            + "<li role=\"none\"><a role=\"menuitem\" class=\"mbi [^\"]+\" href=\"/help\"><span class=\"mh\">Help</span></a></li></menu>$", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-04")]
    public async Task MenuItemsWithCommandsSubmitThemAsync()
    {
        var editor = new Editor();
        const string Body = """
            <Button Content="Actions">
              <Button.Flyout>
                <MenuFlyout>
                  <MenuItem Header="Open" Command="{Binding OpenCommand}" CommandParameter="notes" />
                  <MenuItem Header="Delete" Command="{Binding DeleteCommand}" />
                </MenuFlyout>
              </Button.Flyout>
            </Button>
            """;

        var html = await fixture.HtmlAsync(Body, editor);
        var outcome = await fixture.PostAsync(Body, editor, ("_btn", "0|notes"));

        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|notes\" role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\"><span class=\"mh\">Open</span></button>", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"1\\|\" role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" disabled><span class=\"mh\">Delete</span></button>", html);
        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.Equal("notes", editor.Opened);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-05")]
    public async Task MenuItemsShowIconsGesturesChecksAndSeparatorsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Button Content="More">
              <Button.Flyout>
                <MenuFlyout>
                  <MenuItem Header="_Settings" a:Web.Href="/settings" InputGesture="Ctrl+OemComma">
                    <MenuItem.Icon><a:Icon Kind="settings" /></MenuItem.Icon>
                  </MenuItem>
                  <Separator />
                  <MenuItem Header="Word__wrap" ToggleType="CheckBox" IsChecked="True" />
                  <MenuItem Header="-" />
                </MenuFlyout>
              </Button.Flyout>
            </Button>
            """);

        Assert.Matches("<a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/settings\"><span class=\"mic\" style=\"[^\"]*flex:0 0 16px;[^\"]*\"><svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"16\" height=\"16\"[^>]*>.*?</svg></span><span class=\"mh\">Settings</span><kbd class=\"kg\" style=\"[^\"]*\">Ctrl\\+,</kbd></a><hr class=\"k[0-9a-f]+\">", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|\" role=\"menuitemcheckbox\" aria-checked=\"true\" tabindex=\"-1\" class=\"mi [^\"]+\"><svg aria-hidden=\"true\" style=\"position:absolute;[^\"]*\"[^>]*><g [^>]*><path d=\"[^\"]+\" fill=\"#000000\"[^>]*/></g></svg><span class=\"mh\">Word_wrap</span></button><hr class=\"k[0-9a-f]+\"></div>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-06")]
    public async Task SubmenusAreNestedPopoversAsync()
    {
        var html = await fixture.HtmlAsync("""
            <Menu>
              <MenuItem Header="Edit">
                <MenuItem Header="Find">
                  <MenuItem Header="Next" a:Web.Href="/next" />
                </MenuItem>
              </MenuItem>
            </Menu>
            """);

        Assert.Matches("<button type=\"button\" role=\"menuitem\" aria-haspopup=\"menu\" tabindex=\"-1\" class=\"mi [^\"]+\" popovertarget=\"m2\" style=\"anchor-name:--m2\"><span class=\"mh\">Find</span><svg aria-hidden=\"true\" style=\"flex-shrink:0;margin-left:12px\"[^>]*>.*?</svg></button>"
            + "<div popover id=\"m2\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+\" style=\"position-anchor:--m2;position-area:right span-bottom;margin-left:-4px;position-try-fallbacks:flip-inline;\"><a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/next\"><span class=\"mh\">Next</span></a></div></div>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-07")]
    public async Task FlyoutsArePopoversWithThePresenterLookAsync()
    {
        var page = await fixture.CompileAsync("""
            <Button Content="Info">
              <Button.Flyout>
                <Flyout Placement="BottomEdgeAlignedRight"><TextBlock Text="Details" /></Flyout>
              </Button.Flyout>
            </Button>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<button type=\"button\" popovertarget=\"m1\" class=\"[^\"]+\" style=\"anchor-name:--m1\">Info</button>"
            + "<div popover id=\"m1\" role=\"dialog\" class=\"fly (k[0-9a-f]+)\" style=\"position-anchor:--m1;position-area:bottom span-left;position-try-fallbacks:flip-block;\"><div[^>]*>Details</div></div>", html);
        var look = System.Text.RegularExpressions.Regex.Match(html, "class=\"fly (k[0-9a-f]+)\"").Groups[1].Value;
        Assert.Matches($"\\.{look}\\{{width:max-content;min-width:96px;max-width:456px;background:#f2f2f2;border-color:[^;]+;border-width:1px 1px 1px 1px;border-style:solid;border-radius:5px 5px 5px 5px;padding:11px 12px 12px 12px;box-shadow:none;", page.Css);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-08")]
    public async Task ToolTipsAreCssBubblesAsync()
    {
        var page = await fixture.CompileAsync("""<Button Content="Save" ToolTip.Tip="Saves the file" ToolTip.ShowDelay="250" />""");

        var html = EngineFixture.Body(page);
        Assert.Matches("^<span class=\"tip\" data-aioros-overlay data-between=\"100\"><button type=\"submit\" name=\"_btn\" value=\"0\\|\" aria-describedby=\"m1\"[^>]*>Save</button><span role=\"tooltip\" id=\"m1\" class=\"tt k[0-9a-f]+ k[0-9a-f]+\" style=\"[^\"]+\">Saves the file</span></span>$", html);
        Assert.Contains(".tip{display:contents;", page.Css, StringComparison.Ordinal);
        Assert.Contains(".tip:hover>.tt,.tip:has(:focus-visible)>.tt{opacity:1;visibility:visible;", page.Css, StringComparison.Ordinal);
        Assert.Matches("\\.k[0-9a-f]+\\{--tip-delay:250ms;\\}", page.Css);
        Assert.Matches("\\.k[0-9a-f]+\\{width:max-content;max-width:320px;background:#f2f2f2;border-color:[^;]+;border-width:1px 1px 1px 1px;border-style:solid;border-radius:5px 5px 5px 5px;padding:5px 8px 7px 8px;box-shadow:none;font-size:12px;", page.Css);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-47")]
    public async Task BoundToolTipsFollowTheirTextAsync()
    {
        const string Body = """<Border Padding="4" ToolTip.Tip="{Binding Description}"><TextBlock Text="Badge" /></Border>""";
        var models = new[] { new Described(null), new Described("A category") };
        foreach (var compiled in models)
        {
            var planned = await fixture.CompileAsync(Body, compiled);
            Assert.True(planned.Reusable, planned.Failure);
            foreach (var model in models)
            {
                Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Body, model), model), EngineFixture.Body(planned, model));
            }
        }

        Assert.Contains(">A category</span></span>", await fixture.HtmlAsync(Body, models[1]), StringComparison.Ordinal);
        Assert.DoesNotContain("tooltip", await fixture.HtmlAsync(Body, models[0]), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-71")]
    public async Task ConvertedToolTipsFollowTheirTextAsync()
    {
        const string Body = """<Border Padding="4" ToolTip.Tip="{Binding Description, Converter={x:Static m:Loud.Instance}}"><TextBlock Text="Badge" /></Border>""";
        var models = new[] { new Described(null), new Described("A category") };
        foreach (var compiled in models)
        {
            var planned = await fixture.CompileAsync(Body, compiled);
            var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
            Assert.True(planned.Reusable, planned.Failure);
            foreach (var model in models)
            {
                var expected = EngineFixture.Body(await fixture.CompileAsync(Body, model), model);
                Assert.Equal(expected, EngineFixture.Body(planned, model));
                Assert.Equal(expected, EngineFixture.Body(saved, model));
            }
        }

        Assert.Contains(">A CATEGORY</span></span>", await fixture.HtmlAsync(Body, models[1]), StringComparison.Ordinal);
        Assert.DoesNotContain("tooltip", await fixture.HtmlAsync(Body, models[0]), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-48")]
    public async Task ErrorsSetInTheXamlAreWrittenAsRenderedAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBox Width="200" DataValidationErrors.Error="{x:Static m:NavigationSamples.Invalid}" />
              <NumericUpDown Width="200" DataValidationErrors.Error="{x:Static m:NavigationSamples.Invalid}" />
              <TextBlock Text="{Binding Text}" />
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Body, Tile.On);

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var model in new[] { Tile.On, Tile.Off })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Body, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(2, Alerts(expected).Count);
        }
    }

    [Fact]
    [Trait("AC", "NAVIGATION-09")]
    public async Task NotificationCardsAreStatusCardsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.DataTemplates>
                <DataTemplate DataType="Notification"><StackPanel><TextBlock Text="{Binding Title}" /><TextBlock Text="{Binding Message}" /></StackPanel></DataTemplate>
              </StackPanel.DataTemplates>
              <NotificationCard NotificationType="Success" Content="{x:Static m:NavigationSamples.Saved}" />
              <NotificationCard NotificationType="Error" Content="Failed" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Contains(">Saved</div>", html, StringComparison.Ordinal);
        Assert.Contains(">The file was saved.</div>", html, StringComparison.Ordinal);
        Assert.Contains(">Failed</div>", html, StringComparison.Ordinal);
        Assert.Matches("height:4px;[^}]*background:#128b44;", page.Css);
        Assert.Matches("height:4px;[^}]*background:#f03916;", page.Css);
        Assert.Contains("box-shadow:0px 6px 8px 0px rgba(0,0,0,0.31);", page.Css, StringComparison.Ordinal);
        Assert.DoesNotMatch("opacity:0\\.0|transform:matrix\\(0\\.85", page.Css);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-13")]
    public async Task ValidationErrorsFollowTheirFieldsAsync()
    {
        var order = new ValidatedOrder();
        var registration = new Registration();

        var rejected = await fixture.PostAsync("""<TextBox Text="{Binding Quantity}" />""", order, ("f0", "0"));
        var planned = await fixture.CompileAsync("""<TextBox Text="{Binding Name}" />""", registration);
        var withButton = await fixture.CompileAsync("""<StackPanel><TextBox Text="{Binding Name}" /><Button Content="Save" Command="{Binding SubmitCommand}" /><Button Content="Hover me" ToolTip.Tip="A tip" /></StackPanel>""", new Registration());
        var invalid = await fixture.PostAsync("""<TextBox Text="{Binding Name}" />""", registration, ("f0", string.Empty));
        var applied = new Registration();
        var status = await Runtime.Rendering.PlanForms.ApplyAsync(planned.Plan, [applied], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", string.Empty), new Plans.AdapterSlotResolver());

        Assert.Matches("^<div class=\"wb[^\"]*\"><input [^>]*name=\"f0\" aria-invalid=\"true\" aria-describedby=\"e0\"[^>]*><div id=\"e0\" class=\"vw\"><div role=\"alert\" class=\"ve[0-9a-f]+\">Order at least one. \\(Parameter 'value'\\)</div></div></div>$", EngineFixture.Body(rejected.Page!, order));
        Assert.Equal(1, order.Quantity);
        Assert.True(planned.Reusable, planned.Failure);
        Assert.DoesNotContain("role=\"alert\"", EngineFixture.Body(planned, new Registration()), StringComparison.Ordinal);
        Assert.Matches("<div role=\"alert\" class=\"ve[0-9a-f]+\">A name is required.</div></div></div>$", EngineFixture.Body(invalid.Page!, registration));
        Assert.Equal(Runtime.Rendering.PostStatus.Rendered, status);
        Assert.Equal(Alerts(EngineFixture.Body(invalid.Page!, registration)), Alerts(EngineFixture.Body(planned, applied)));
        Assert.Matches("\\[aria-invalid=\"true\"\\]\\{[^}]*border-color:[^}]+\\}", planned.Css);
        Assert.True(withButton.Reusable, withButton.Failure);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-46")]
    public async Task ValidatingModelsPlanTheErrorsOfEveryValidatedBindingAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBox IsReadOnly="True" Text="{Binding Code, Mode=OneWay}" />
              <TextBox Text="{Binding Name}" />
              <CalendarDatePicker SelectedDate="{Binding Start}" />
              <ContentControl Tag="inner">
                <ContentControl.Template>
                  <ControlTemplate><TextBox Text="{TemplateBinding Tag}" /></ControlTemplate>
                </ContentControl.Template>
              </ContentControl>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Body, new Enrollment());
        var expected = EngineFixture.Body(await fixture.CompileAsync(Body, Enrollment.Invalid()), Enrollment.Invalid());

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(["A code has three letters.", "A name is required.", "A start is required."], Alerts(expected));
        Assert.Equal(Alerts(expected), Alerts(EngineFixture.Body(planned, Enrollment.Invalid())));
        Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Body, new Enrollment()), new Enrollment()), EngineFixture.Body(planned, new Enrollment()));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-81")]
    public async Task TemplatePartsBoundToTheirControlShowNoModelErrorsAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBox Text="{Binding Name}" />
              <ContentControl Tag="Blue">
                <ContentControl.Template>
                  <ControlTemplate>
                    <ComboBox ItemsSource="{x:Static m:Swatches.Names}" SelectedItem="{Binding RelativeSource={RelativeSource TemplatedParent}, Path=Tag, Mode=TwoWay}" />
                  </ControlTemplate>
                </ContentControl.Template>
              </ContentControl>
            </StackPanel>
            """;
        var planned = await fixture.CompileAsync(Body, new Enrollment());
        var expected = EngineFixture.Body(await fixture.CompileAsync(Body, Enrollment.Invalid()), Enrollment.Invalid());

        Assert.True(planned.Reusable, planned.Failure);
        Assert.Equal(["A name is required."], Alerts(expected));
        Assert.Equal(Alerts(expected), Alerts(EngineFixture.Body(planned, Enrollment.Invalid())));
        Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Body, new Enrollment()), new Enrollment()), EngineFixture.Body(planned, new Enrollment()));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-10")]
    public async Task CommandBarsAreToolbarsAsync()
    {
        var editor = new Editor();
        const string Body = """
            <CommandBar>
              <CommandBar.Content><TextBlock Text="Document" /></CommandBar.Content>
              <CommandBarButton Label="Save" Command="{Binding SaveCommand}"><CommandBarButton.Icon><a:Icon Kind="check" /></CommandBarButton.Icon></CommandBarButton>
              <CommandBarSeparator />
              <CommandBar.SecondaryCommands>
                <CommandBarButton Label="Delete" Command="{Binding DeleteCommand}" />
                <CommandBarSeparator />
                <CommandBarButton Label="Open" Command="{Binding OpenCommand}" CommandParameter="recent" />
              </CommandBar.SecondaryCommands>
            </CommandBar>
            """;

        var html = await fixture.HtmlAsync(Body, editor);
        await fixture.PostAsync(Body, editor, ("_btn", "2|recent"));

        Assert.Matches("^<menu class=\"cb k[0-9a-f]+\"><li class=\"cbc\"><div[^>]*>Document</div></li>"
            + "<li><button type=\"submit\" name=\"_btn\" value=\"0\\|\" class=\"cbb [^\"]+\"><svg [^>]*width=\"16\"[^>]*>.*?</svg><span class=\"cbl\">Save</span></button></li>"
            + "<li role=\"separator\" class=\"cbs\" style=\"background:[^\"]+\"></li>"
            + "<li><button type=\"button\" class=\"cbb cbm\" popovertarget=\"m1\" style=\"anchor-name:--m1;[^\"]*\" aria-label=\"More\"><svg[^>]*>.*?</svg></button>"
            + "<div popover id=\"m1\" class=\"menu mn[0-9a-f]+\" style=\"position-anchor:--m1;[^\"]*\"><button type=\"submit\" name=\"_btn\" value=\"1\\|\" class=\"mi\" disabled><span>Delete</span></button><hr>"
            + "<button type=\"submit\" name=\"_btn\" value=\"2\\|recent\" class=\"mi\"><span>Open</span></button></div></li></menu>$", html);
        Assert.Equal("recent", editor.Opened);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-11")]
    public async Task CommandBarToggleButtonsAreCheckboxFieldsAsync()
    {
        var editor = new Editor();
        const string Body = """<CommandBarToggleButton Label="Bold" IsChecked="{Binding Bold}"><CommandBarToggleButton.Icon><a:Icon Kind="zap" /></CommandBarToggleButton.Icon></CommandBarToggleButton>""";

        var html = await fixture.HtmlAsync(Body, editor);
        await fixture.PostAsync(Body, editor);

        Assert.Matches("^<label class=\"cbb [^\"]*cbt[0-9a-f]+\"><input type=\"checkbox\" class=\"cbx\" name=\"f0\" value=\"on\" checked><svg [^>]*>.*?</svg><span class=\"cbl\">Bold</span></label>$", html);
        Assert.False(editor.Bold);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-12")]
    public async Task TransitioningContentEntersWithACssAnimationAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TransitioningContentControl><TextBlock Text="Faded" /></TransitioningContentControl>
              <TransitioningContentControl>
                <TransitioningContentControl.PageTransition><PageSlide Duration="0:0:0.3" /></TransitioningContentControl.PageTransition>
                <TextBlock Text="Slid" />
              </TransitioningContentControl>
              <TransitioningContentControl PageTransition="{x:Null}"><TextBlock Text="Still" /></TransitioningContentControl>
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var classes = System.Text.RegularExpressions.Regex.Matches(html, "<div class=\"c (k[0-9a-f]+)\"><div[^>]*>(?:Faded|Slid)</div></div>").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(2, classes.Count);
        Assert.Contains($".{classes[0]}{{animation:tcf 125ms ease both;overflow:hidden;}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($".{classes[1]}{{animation:tcsX 300ms linear both;overflow:hidden;}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes tcf{from{opacity:0}}", page.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes tcsX{from{transform:translateX(100%)}}", page.Css, StringComparison.Ordinal);
        Assert.Matches("<div class=\"c\"><div[^>]*>Still</div></div>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-14")]
    public async Task RefreshContainersWriteOnlyTheirContentAsync()
    {
        var html = await fixture.HtmlAsync("""<RefreshContainer><TextBlock Text="Feed" /></RefreshContainer>""");

        Assert.Matches("^<div class=\"k[0-9a-f]+\">Feed</div>$", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-15")]
    public async Task ContentPagesAreSectionsWithTheirBarsAsync()
    {
        var page = await fixture.CompileAsync("""
            <ContentPage Header="Home">
              <ContentPage.TopCommandBar><TextBlock Text="Top" /></ContentPage.TopCommandBar>
              <TextBlock Text="Body" />
              <ContentPage.BottomCommandBar><TextBlock Text="Bottom" /></ContentPage.BottomCommandBar>
            </ContentPage>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("^<section class=\"c (k[0-9a-f]+)\"><div[^>]*>Top</div><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Body</div></div><div[^>]*>Bottom</div></section>$", html);
        Assert.Matches("\\{flex:1 1 0%;min-height:0;background:#ffffff;border-width:0;color:#000000;\\}", page.Css);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-16")]
    public async Task NavigationPagesHaveABarWithTitleAndBackLinkAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <NavigationPage a:Web.Href="/back"><ContentPage Header="Inbox"><TextBlock Text="Messages" /></ContentPage></NavigationPage>
              <NavigationPage><ContentPage Header="Root"><TextBlock Text="Start" /></ContentPage></NavigationPage>
            </StackPanel>
            """);

        Assert.Matches("<div class=\"c[^\"]*\"><header class=\"nb[0-9a-f]+\"><a class=\"nbk\" href=\"/back\" aria-label=\"Back\"><svg[^>]*>.*?</svg></a><h1 class=\"nbt\">Inbox</h1></header><section class=\"c k[0-9a-f]+\"><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Messages</div></div></section></div>", html);
        Assert.Matches("<header class=\"nb[0-9a-f]+\"><h1 class=\"nbt\">Root</h1></header>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-17")]
    public async Task TabbedPagesAreRadioTabsAsync()
    {
        var editor = new Editor();
        const string Body = """
            <TabbedPage SelectedIndex="{Binding Tab, Mode=TwoWay}">
              <ContentPage Header="One"><TextBlock Text="First" /></ContentPage>
              <ContentPage Header="Two"><TextBlock Text="Second" /></ContentPage>
            </TabbedPage>
            """;

        var page = await fixture.CompileAsync(Body, editor);
        var outcome = await fixture.PostAsync(Body, editor, ("f0", "0"));

        var html = EngineFixture.Body(page, editor);
        Assert.Matches("^<div class=\"tp tl[0-9a-f]+[^\"]*\"><div role=\"tablist\" class=\"tps\"><label class=\"tpt\"><input type=\"radio\" class=\"cbx\" name=\"f0\" value=\"0\" checked><span>One</span></label>"
            + "<label class=\"tpt\"><input type=\"radio\" class=\"cbx\" name=\"f0\" value=\"1\"><span>Two</span></label></div>"
            + "<div role=\"tabpanel\" class=\"tpp\"><section[^>]*><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>First</div></div></section></div>"
            + "<div role=\"tabpanel\" class=\"tpp\"><section[^>]*><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Second</div></div></section></div></div>$", html);
        Assert.Contains(".tp:has(>.tps>:nth-child(2) :checked)>.tpp:nth-child(3){display:flex}", page.Css, StringComparison.Ordinal);
        Assert.Equal(0, editor.Tab);
        Assert.Contains("value=\"0\" checked", EngineFixture.Body(outcome.Page!, editor), StringComparison.Ordinal);
        Assert.True(page.Reusable, page.Failure);
        var applied = new Editor();
        var status = await Runtime.Rendering.PlanForms.ApplyAsync(page.Plan, [applied], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", "0"), new Plans.AdapterSlotResolver());
        Assert.Equal((Runtime.Rendering.PostStatus.Rendered, 0), (status, applied.Tab));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-18")]
    public async Task DrawerPagesArePopoverDrawersAsync()
    {
        var page = await fixture.CompileAsync("""
            <DrawerPage a:Web.Id="drawer" DrawerLayoutBehavior="Split" IsOpen="True" DrawerBreakpointLength="700" DrawerLength="260">
              <DrawerPage.DrawerHeader><TextBlock Text="Header" /></DrawerPage.DrawerHeader>
              <DrawerPage.Drawer><TextBlock Text="Links" /></DrawerPage.Drawer>
              <DrawerPage.DrawerFooter><TextBlock Text="Footer" /></DrawerPage.DrawerFooter>
              <ContentPage Header="Detail"><TextBlock Text="Main" /></ContentPage>
            </DrawerPage>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("^<div class=\"k[0-9a-f]+\"><aside popover id=\"drawer\" class=\"pv[0-9a-f]+\"><div[^>]*>Header</div><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Links</div></div><div[^>]*>Footer</div></aside>"
            + "<div class=\"c\" style=\"flex:1 1 0;min-width:0\"><div class=\"db[0-9a-f]+\"><button type=\"button\" popovertarget=\"drawer\" class=\"dbm ha700\" aria-label=\"Toggle navigation drawer\"><svg[^>]*>.*?</svg></button><div class=\"dbt\">Detail</div></div>"
            + "<section[^>]*><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Main</div></div></section></div></div>$", html);
        Assert.Contains("@media (max-width:700px){.pv", page.Css, StringComparison.Ordinal);
        Assert.Contains("width:260px", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-106")]
    public async Task DrawerPagesDockAsAvaloniaResolvesThemAsync()
    {
        string[] drawers =
        [
            "DrawerLayoutBehavior=\"Split\" IsOpen=\"True\"",
            "DrawerLayoutBehavior=\"CompactInline\" IsOpen=\"True\"",
            "DrawerBehavior=\"Locked\"",
            "DrawerBreakpointLength=\"700\" IsOpen=\"True\"",
            "DrawerLayoutBehavior=\"CompactOverlay\" IsOpen=\"True\"",
            "DrawerLayoutBehavior=\"Split\"",
            "DrawerLayoutBehavior=\"Split\" DrawerBehavior=\"Flyout\" IsOpen=\"True\"",
        ];
        var docked = new List<bool>();
        foreach (var drawer in drawers)
        {
            var page = await fixture.CompileAsync($"""<DrawerPage {drawer} DrawerLength="300"><DrawerPage.Drawer><TextBlock Text="Links" /></DrawerPage.Drawer><ContentPage><TextBlock Text="Main" /></ContentPage></DrawerPage>""");
            docked.Add(page.Css.Contains("@media (min-width:0.02px){.pv", StringComparison.Ordinal));
        }

        Assert.Equal([true, true, true, false, false, false, false], docked);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-19")]
    public async Task CarouselPagesAreScrollSnapStripsAsync()
    {
        var editor = new Editor();
        var page = await fixture.CompileAsync("""
            <CarouselPage SelectedIndex="{Binding Tab}">
              <ContentPage><TextBlock Text="Slide 1" /></ContentPage>
              <ContentPage><TextBlock Text="Slide 2" /></ContentPage>
            </CarouselPage>
            """, editor);

        var html = EngineFixture.Body(page, editor);
        Assert.Matches("^<div class=\"cp[^\"]*\"><div class=\"cpp\"><section[^>]*><div[^>]*><div[^>]*>Slide 1</div></div></section></div><div class=\"cpp cpi\"><section[^>]*><div[^>]*><div[^>]*>Slide 2</div></div></section></div></div>$", html);
        Assert.Contains(".cp{display:flex;flex-direction:row;overflow-x:auto;scroll-snap-type:x mandatory;", page.Css, StringComparison.Ordinal);
        Assert.Contains(".cp>.cpp{flex:0 0 100%;min-width:0;scroll-snap-align:start;", page.Css, StringComparison.Ordinal);
        Assert.Contains(".cp>.cpi{scroll-initial-target:nearest}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-20")]
    public async Task PageNavigationHostsWriteTheirPageAsync()
    {
        var html = await fixture.HtmlAsync("""<PageNavigationHost><PageNavigationHost.Page><ContentPage><TextBlock Text="Hosted" /></ContentPage></PageNavigationHost.Page></PageNavigationHost>""");

        Assert.Matches("^<section class=\"c k[0-9a-f]+\"><div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Hosted</div></div></section>$", html);
    }

    private static List<string> Alerts(string html) =>
        [.. System.Text.RegularExpressions.Regex.Matches(html, "<div role=\"alert\"[^>]*>([^<]*)</div>").Select(match => match.Groups[1].Value)];

    [Fact]
    [Trait("AC", "NAVIGATION-21")]
    public async Task LinksToTheCurrentPageAreStyledAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="Border.nav:current"><Setter Property="Background" Value="#123456" /></Style>
                <Style Selector="Border.nav:current TextBlock"><Setter Property="Foreground" Value="#ABCDEF" /></Style>
              </StackPanel.Styles>
              <Border Classes="nav" a:Web.Href="/games"><TextBlock Text="Games" /></Border>
              <Border Classes="nav" a:Web.Href="{Binding Title}"><TextBlock Text="Bound" /></Border>
              <Border Classes="nav" a:Web.Href="/games/all" a:Web.Exact="True"><TextBlock Text="All" /></Border>
              <Border Classes="plain" a:Web.Href="/games"><TextBlock Text="Plain" /></Border>
            </StackPanel>
            """, new Article("/crm"));
        var games = EngineFixture.BodyAt(page, "/games/12", new Article("/crm"));
        var crm = EngineFixture.BodyAt(page, "/crm", new Article("/crm"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("<a href=\"/games\" aria-current=\"page\"[^>]*class=\"[^\"]*\">", games);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(games, "href=\"/games\" aria-current"));
        Assert.Matches("<a href=\"/games\" aria-current=\"page\" data-nav class=\"[^\"]*\"><div[^>]*>Plain</div></a>", games);
        Assert.DoesNotContain("href=\"/crm\" aria-current", games, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/games/all\" aria-current", EngineFixture.BodyAt(page, "/games/all/1", new Article("/crm")), StringComparison.Ordinal);
        Assert.Contains("href=\"/games/all\" aria-current", EngineFixture.BodyAt(page, "/games/all", new Article("/crm")), StringComparison.Ordinal);
        Assert.Contains("href=\"/crm\" aria-current=\"page\"", crm, StringComparison.Ordinal);
        Assert.Matches("\\[aria-current=\"page\"\\]\\{[^}]*background:#123456", page.Css);
        Assert.Matches("\\[aria-current=\"page\"\\] \\.d[0-9a-f]+\\{[^}]*color:#abcdef", page.Css);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-22")]
    public async Task CommandBarIconsKeepTheirSizeAsync()
    {
        const string Body = """
            <StackPanel Orientation="Horizontal">
              <CommandBar><CommandBarButton Label="Small"><CommandBarButton.Icon><a:Icon Kind="plus" /></CommandBarButton.Icon></CommandBarButton></CommandBar>
              <CommandBar><CommandBarButton Label="Large"><CommandBarButton.Icon><a:Icon Kind="plus" Size="24" /></CommandBarButton.Icon></CommandBarButton></CommandBar>
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;

        Assert.Matches("<svg [^>]*width=\"16\" height=\"16\"[^>]*>.*?</svg><span class=\"cbl\">Small</span>", html);
        Assert.Matches("<svg [^>]*width=\"24\" height=\"24\"[^>]*>.*?</svg><span class=\"cbl\">Large</span>", html);
        var small = laid.Single(text => text.Text == "Small");
        var large = laid.Single(text => text.Text == "Large");
        Assert.Equal((24 - 16) / 2.0, large.Y - small.Y);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-23")]
    public async Task ContentPageThemesInsetTheContentAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Inset" TargetType="ContentPage">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <DockPanel>
                        <ScrollViewer Margin="20,20,5,0">
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                        </ScrollViewer>
                      </DockPanel>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <ContentPage Theme="{StaticResource Inset}" Height="60"><TextBlock Text="Inset" /></ContentPage>
              <ContentPage Height="60"><TextBlock Text="Plain" /></ContentPage>
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;

        Assert.Contains("<div class=\"c\" style=\"flex:1 0 auto;padding:20px 5px 0px 20px\"><div class=\"k", html, StringComparison.Ordinal);
        Assert.Matches("<div class=\"c\" style=\"flex:1 0 auto\"><div[^>]*>Plain</div>", html);
        Assert.Equal([(20.0, 20.0), (0.0, 60.0)], laid.Select(text => (text.X, text.Y)));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-24")]
    public async Task SharedMenuFlyoutsRunTheirItemsCommandAsync()
    {
        const string Body = """
            <StackPanel x:Name="Root">
              <StackPanel.Resources>
                <MenuFlyout x:Key="Actions" x:DataType="m:MenuRow">
                  <MenuItem Header="Open" Command="{Binding #Root.((m:MenuRows)DataContext).OpenCommand}" CommandParameter="{Binding}" />
                </MenuFlyout>
              </StackPanel.Resources>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:MenuRow">
                    <Button Content="{Binding Name}" Flyout="{StaticResource Actions}" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """;
        var second = ("_btn", "1|" + typeof(MenuRow).FullName);
        var rendered = new MenuRows();
        var page = await fixture.CompileAsync(Body, rendered);
        var posted = new MenuRows();

        var outcome = await fixture.PostAsync(Body, posted, second);

        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(EngineFixture.Body(page, rendered), "name=\"_btn\" value=\"[012]\\|"));
        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.Equal(["Second"], posted.Opened);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-70")]
    public async Task PopoversInListRowsAreNumberedPerRowAsync()
    {
        const string Body = """
            <StackPanel>
              <Button Content="Before"><Button.Flyout><MenuFlyout><MenuItem Header="One" /></MenuFlyout></Button.Flyout></Button>
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:MenuRow">
                    <Button Content="{Binding Name}">
                      <Button.Flyout><MenuFlyout><MenuItem Header="Open" /></MenuFlyout></Button.Flyout>
                    </Button>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <DropDownButton Content="After"><DropDownButton.Flyout><Flyout><TextBlock Text="Inside" /></Flyout></DropDownButton.Flyout></DropDownButton>
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body, new MenuRows("Solo"));
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", page)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(page.Reusable, page.Failure);
        foreach (var model in new[] { new MenuRows("Solo"), new MenuRows(), new MenuRows("A", "B", "C", "D") })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Body, model), model);
            Assert.Equal(expected, EngineFixture.Body(page, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        var four = EngineFixture.Body(page, new MenuRows("A", "B", "C", "D"));
        Assert.Equal(6, Regex.Matches(four, "popovertarget=\"(m\\d+)\"").Select(match => match.Groups[1].Value).Distinct().Count());
        Assert.Contains("popovertarget=\"m6\"", four, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-98")]
    public async Task APostFromAFlyoutKeepsItOpenAsync()
    {
        const string Body = """
            <StackPanel>
              <Button Content="Sort">
                <Button.Flyout>
                  <Flyout>
                    <StackPanel>
                      <ComboBox ItemsSource="{Binding Options}" SelectedItem="{Binding Sort}" />
                      <Button Content="Apply" Command="{Binding ApplyCommand}" />
                      <Button Content="Step" />
                    </StackPanel>
                  </Flyout>
                </Button.Flyout>
              </Button>
              <Button Content="Outside" Command="{Binding ApplyCommand}" />
            </StackPanel>
            """;
        var html = await fixture.HtmlAsync(Body, new SortChoice());
        string Id(string text) => System.Text.RegularExpressions.Regex.Match(html, "value=\"([0-9]+)\\|\"[^>]*>(<[^>]+>)*" + text).Groups[1].Value + "|";
        async Task<bool> OpenAsync(params (string, string)[] form)
        {
            var choice = new SortChoice();
            var outcome = await fixture.PostAsync(Body, choice, form);
            return EngineFixture.Body(outcome.Page!, choice).Contains("data-aioros-open", StringComparison.Ordinal);
        }

        Assert.Equal((true, true, false, false), (await OpenAsync(("f0", "1"), ("_btn", Id("Step"))), await OpenAsync(("f0", "1"), ("_chg", "0")), await OpenAsync(("f0", "1"), ("_btn", Id("Outside"))), await OpenAsync(("f0", "1"), ("_btn", Id("Apply")))));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-25")]
    public async Task FlyoutFieldsBindWhenPostedAsync()
    {
        const string Body = """
            <Button Content="Sort">
              <Button.Flyout>
                <Flyout>
                  <StackPanel>
                    <ComboBox ItemsSource="{Binding Options}" SelectedItem="{Binding Sort}" />
                    <Button Content="Apply" Command="{Binding ApplyCommand}" />
                  </StackPanel>
                </Flyout>
              </Button.Flyout>
            </Button>
            """;
        var html = await fixture.HtmlAsync(Body, new SortChoice());
        var choice = new SortChoice();

        var outcome = await fixture.PostAsync(Body, choice, ("f0", "1"), ("_btn", "0|"));

        Assert.Contains("<select name=\"f0\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"_btn\" value=\"0|\"", html, StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.Equal("Title", choice.Sort);
        Assert.Equal(1, choice.Applied);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-26")]
    public async Task TemplatedControlsInFlyoutsAreWrittenWithTheirTemplateAsync()
    {
        const string Labeled = """
            <HeaderedContentControl Header="Sort by">
              <HeaderedContentControl.Template>
                <ControlTemplate>
                  <StackPanel Spacing="4">
                    <ContentPresenter Content="{TemplateBinding Header}" />
                    <ContentPresenter Content="{TemplateBinding Content}" />
                  </StackPanel>
                </ControlTemplate>
              </HeaderedContentControl.Template>
              <TextBlock Text="Due date" />
            </HeaderedContentControl>
            """;

        var outside = EngineFixture.Body(await fixture.CompileAsync(Labeled));
        var page = await fixture.CompileAsync($"<Button Content=\"Sort\"><Button.Flyout><Flyout>{Labeled}</Flyout></Button.Flyout></Button>");
        var inside = Regex.Match(EngineFixture.Body(page), "<div popover id=\"m1\"[^>]*>(?<content>.*)</div>$").Groups["content"].Value;

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains(">Sort by<", inside, StringComparison.Ordinal);
        Assert.Contains(">Due date<", inside, StringComparison.Ordinal);
        Assert.Equal(Regex.Replace(outside, "class=\"[^\"]*\"", string.Empty), Regex.Replace(inside, "class=\"[^\"]*\"", string.Empty));
    }

    [Fact]
    [Trait("AC", "NAVIGATION-91")]
    public async Task ThemedExpandersAreWrittenThroughTheirTemplateAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Trigger" TargetType="ToggleButton">
                  <Setter Property="Background" Value="Transparent" />
                  <Setter Property="Padding" Value="0,16" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="16">
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" />
                          <Viewbox Name="PART_Chevron" Grid.Column="1" Width="16" Height="16" RenderTransformOrigin="50%,50%">
                            <Path Data="M6 9 L12 15 L18 9" Stroke="#737373" StrokeThickness="2" />
                          </Viewbox>
                        </Grid>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:checked /template/ Viewbox#PART_Chevron"><Setter Property="RenderTransform" Value="rotate(180deg)" /></Style>
                  <Style Selector="^:pointerover"><Setter Property="TextBlock.TextDecorations" Value="Underline" /></Style>
                </ControlTheme>
                <ControlTheme x:Key="Item" TargetType="Expander">
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                  <Setter Property="BorderThickness" Value="0,0,0,1" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                        <StackPanel>
                          <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Trigger}" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}"
                                        Content="{TemplateBinding Header}" ContentTemplate="{TemplateBinding HeaderTemplate}" />
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}"
                                            IsVisible="{TemplateBinding IsExpanded}" Padding="0,0,0,16" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <StackPanel a:Web.Exclusive="True">
                <Expander Theme="{StaticResource Item}" Header="Is it accessible?" IsExpanded="True" Content="Yes." />
                <Expander Theme="{StaticResource Item}" Header="Is it styled?" Content="Yes, too." />
              </StackPanel>
              <Expander Header="Fluent" Content="As before" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        var items = Regex.Matches(html, "<details class=\"([^\"]+)\"( open)? name=\"([^\"]+)\"><summary class=\"([^\"]+)\">((?:(?!</summary>).)*)</summary><div class=\"([^\"]+)\">((?:(?!</details>).)*)</div></details>").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(" open", items[0].Groups[2].Value);
        Assert.Equal(items[0].Groups[3].Value, items[1].Groups[3].Value);
        Assert.Matches("^(?!.*chev).*Is it accessible\\?.*<svg.*</svg>", items[0].Groups[5].Value);
        Assert.Contains("Yes.", items[0].Groups[7].Value, StringComparison.Ordinal);
        Assert.Matches("border-width:0px 0px 1px 0px", Rules(page, items[0].Groups[1].Value));
        Assert.Matches("padding:16px 0px 16px 0px;[^}]*display:block;", Rules(page, items[0].Groups[4].Value));
        Assert.Matches(":hover\\{[^}]*text-decoration:underline", Rules(page, items[0].Groups[4].Value));
        Assert.Matches("padding:0px 0px 16px 0px", Rules(page, items[0].Groups[6].Value));
        Assert.Matches("details\\[open\\]>summary \\.[a-z0-9]+\\{transform:[^}]+\\}", page.Css);
        Assert.Matches("<details class=\"c[^\"]*\"><summary[^>]*><svg class=\"chev\"[^>]*>.*?</svg>Fluent</summary>", html);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-92")]
    public async Task DataItemsOfExpanderListsTakeTheirTemplatesAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Trigger" TargetType="ToggleButton">
                  <Setter Property="Background" Value="Transparent" />
                  <Setter Property="Padding" Value="0,16" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="16">
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" />
                          <Viewbox Name="PART_Chevron" Grid.Column="1" Width="16" Height="16" RenderTransformOrigin="50%,50%">
                            <Path Data="M6 9 L12 15 L18 9" Stroke="#737373" StrokeThickness="2" />
                          </Viewbox>
                        </Grid>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:checked /template/ Viewbox#PART_Chevron"><Setter Property="RenderTransform" Value="rotate(180deg)" /></Style>
                  <Style Selector="^:pointerover"><Setter Property="TextBlock.TextDecorations" Value="Underline" /></Style>
                </ControlTheme>
                <ControlTheme x:Key="Item" TargetType="Expander">
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                  <Setter Property="BorderThickness" Value="0,0,0,1" />
                  <Setter Property="HorizontalAlignment" Value="Stretch" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                        <StackPanel>
                          <ToggleButton Name="ExpanderHeader" Theme="{StaticResource Trigger}" IsChecked="{TemplateBinding IsExpanded, Mode=TwoWay}"
                                        Content="{TemplateBinding Header}" ContentTemplate="{TemplateBinding HeaderTemplate}" />
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}"
                                            IsVisible="{TemplateBinding IsExpanded}" Padding="0,0,0,16" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <StackPanel a:Web.Exclusive="True">
                <m:Folds ItemsSource="{Binding Crumbs}" ItemContainerTheme="{StaticResource Item}">
                  <m:Folds.ItemTemplate><DataTemplate x:DataType="m:Crumb"><TextBlock Text="{Binding Name, StringFormat='About {0}'}" /></DataTemplate></m:Folds.ItemTemplate>
                  <m:Folds.ContentTemplate><DataTemplate x:DataType="m:Crumb"><TextBlock Text="{Binding Href}" /></DataTemplate></m:Folds.ContentTemplate>
                </m:Folds>
              </StackPanel>
            </StackPanel>
            """;
        var models = new[] { new Trail(new Crumb("Docs", "/docs"), new Crumb("Blog", "/blog")), new Trail(new Crumb("Shop", "/shop")), new Trail(new Crumb("A", "/a"), new Crumb("B", "/b"), new Crumb("C", "/c")) };

        var page = await fixture.PlansLikeAvaloniaAsync(Body, models);

        var html = EngineFixture.Body(page, models[2]);
        Assert.Equal(3, Regex.Count(html, "<details [^>]*name=\"x1\""));
        Assert.Matches("<summary[^>]*>.*About B.*</summary><div[^>]*><div[^>]*>/b</div></div></details>", html);
        Assert.DoesNotContain("Crumb", html, StringComparison.Ordinal);
    }

    private static string Rules(Runtime.Rendering.CompiledPage page, string classes) =>
        string.Join('\n', page.Css.Split('\n').Where(rule => classes.Split(' ').Any(name => rule.StartsWith($".{name}{{", StringComparison.Ordinal) || rule.StartsWith($".{name}:", StringComparison.Ordinal))));

    [Fact]
    [Trait("AC", "NAVIGATION-95")]
    public async Task ACollapsibleIsWrittenThroughItsPanelTemplateAsync()
    {
        static string Collapsible(bool open) => $$"""
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/CollapsibleTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <Expander Theme="{StaticResource CosmoCollapsible}" Width="350" Padding="0,0,16,0" IsExpanded="{{open}}">
                <Expander.Header><TextBlock Text="@peduarte starred 3 repositories" /></Expander.Header>
                <TextBlock Text="@radix-ui/colors" />
              </Expander>
            </StackPanel>
            """;

        var open = await fixture.CompileAsync(Collapsible(true));
        var closed = await fixture.CompileAsync(Collapsible(false));
        var html = EngineFixture.Body(open);
        var hover = Regex.Match(open.Css, @"details>summary:not\(:disabled\):hover \.(?<name>[a-z0-9]+)\{background:#f5f5f5!important;").Groups["name"].Value;
        var content = Regex.Match(html, "</summary><div class=\"(?<class>[^\"]+)\">").Groups["class"].Value.Split(' ')[^1];

        Assert.Matches("<details class=\"[^\"]+\" open><summary class=\"[^\"]+\">", html);
        Assert.DoesNotContain("chev", html, StringComparison.Ordinal);
        Assert.NotEmpty(hover);
        Assert.Matches($"<div class=\"[^\"]*{hover}[^\"]*\"><svg", html);
        Assert.Matches($"\\.{content}\\{{[^}}]*margin-top:8px", open.Css);
        Assert.Contains("@peduarte starred 3 repositories", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" open>", EngineFixture.Body(closed), StringComparison.Ordinal);
    }
    [Fact]
    [Trait("AC", "NAVIGATION-107")]
    public async Task LinksWriteOnlySafeAddressesAsync()
    {
        const string Links = """<StackPanel><Border a:Web.Href="{Binding Name}"><TextBlock Text="box" /></Border><TextBlock><Run Text="see " /><Run a:Web.Href="{Binding Name}" Text="run" /></TextBlock><HyperlinkButton NavigateUri="javascript:alert(2)" Content="link" /></StackPanel>""";
        string[] addresses = ["/issues/7", "https://example.com/a?b=1", "mailto:ada@example.com", "tel:+15551234", "#top", "javascript:alert(1)", " JaVaScRiPt:alert(1)", "java\tscript:alert(1)", "java\nscript:alert(1)", "data:text/html,<script>alert(1)</script>", "vbscript:msgbox(1)"];
        var page = await fixture.PlansLikeAvaloniaAsync(Links, [.. addresses.Select(address => (object)new Editing { Name = address })]);

        var written = addresses.Select(address => Regex.Matches(EngineFixture.Body(page, new Editing { Name = address }), "href=\"([^\"]*)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList()).ToList();

        Assert.All(written, hrefs => Assert.Equal(3, hrefs.Count));
        Assert.Equal(["/issues/7", "https://example.com/a?b=1", "mailto:ada@example.com", "tel:+15551234", "#top", "#", "#", "#", "#", "#", "#"], written.Select(hrefs => hrefs[0]));
        Assert.All(written, hrefs => Assert.Equal(hrefs[0], hrefs[1]));
        Assert.All(written, hrefs => Assert.Equal("#", hrefs[2]));
    }
}
