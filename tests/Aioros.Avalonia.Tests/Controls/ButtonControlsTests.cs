using System.Text.RegularExpressions;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ButtonControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "BUTTONS-123")]
    public async Task StackedLinesInAButtonStartAtItsLeftAsync()
    {
        const string Body = """
            <Button Width="240" HorizontalContentAlignment="Stretch">
              <Button.Template>
                <ControlTemplate>
                  <Panel>
                    <Border Name="PART_Border" Padding="8,6">
                      <DockPanel>
                        <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}" />
                      </DockPanel>
                    </Border>
                  </Panel>
                </ControlTemplate>
              </Button.Template>
              <Button.Flyout><MenuFlyout><MenuItem Header="Account" /></MenuFlyout></Button.Flyout>
              <DockPanel>
                <Border DockPanel.Dock="Right" Width="16" Height="16" Margin="8,0,0,0" />
                <Border Width="32" Height="32" />
                <StackPanel Margin="8,0,0,0" VerticalAlignment="Center">
                  <TextBlock Text="shadcn" FontWeight="Medium" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="m@example.com" FontSize="12" TextTrimming="CharacterEllipsis" />
                </StackPanel>
              </DockPanel>
            </Button>
            """;
        var desktop = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 400, 200))).Texts;
        var web = await BrowserLayout.TextsAsync(await fixture.CompileAsync(Body), 400, 200);
        static double Offset(System.Collections.Immutable.ImmutableArray<LaidOutText> texts) =>
            texts.Single(text => text.Text == "shadcn").X - texts.Single(text => text.Text == "m@example.com").X;

        Assert.Equal(0, Offset(desktop), 1);
        Assert.Equal(0, Offset(web), 1);
    }

    [Fact]
    [Trait("AC", "BUTTONS-01")]
    public async Task ButtonsSubmitTheirCommandAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Button Content="Ping" Command="{Binding PingCommand}" CommandParameter="7" />
              <Button Content="Off" IsEnabled="False" />
            </StackPanel>
            """, new Profile());

        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|7\" class=\"[^\"]+\">Ping</button>", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"1\\|\" class=\"[^\"]+\" disabled>Off</button>", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-02")]
    public async Task HyperlinkButtonsAreLinksAsync()
    {
        var page = await fixture.CompileAsync("""<HyperlinkButton NavigateUri="https://avaloniaui.net/" Content="Avalonia" Foreground="#123456" />""");

        Assert.Matches("<a href=\"https://avaloniaui.net/\" class=\"lk [s0-9a-f ]*k[0-9a-f]+\">Avalonia</a>", EngineFixture.Body(page));
        Assert.Contains("color:#123456;", page.Css, StringComparison.Ordinal);
        Assert.Contains("text-decoration:underline;", page.Css, StringComparison.Ordinal);
        Assert.Matches("padding:[0-9.]+px", page.Css);
    }

    [Fact]
    [Trait("AC", "BUTTONS-03")]
    public async Task CheckBoxesAreCheckboxesAsync()
    {
        var profile = new Profile();
        const string Body = """<CheckBox Content="Agree" IsChecked="{Binding Agree}" />""";

        var html = await fixture.HtmlAsync(Body, profile);
        await fixture.PostAsync(Body, profile);

        Assert.Matches("<label class=\"[^\"]+\"><input type=\"checkbox\" name=\"f0\" value=\"on\" checked>Agree</label>", html);
        Assert.False(profile.Agree);
    }

    [Fact]
    [Trait("AC", "BUTTONS-04")]
    public async Task PopoverButtonsAreNotFieldsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <Button a:Web.Toggles="nav" Content="Menu" />
              <Button Content="Account">
                <Button.Flyout>
                  <MenuFlyout>
                    <MenuItem Header="Profile" a:Web.Href="/profile" />
                  </MenuFlyout>
                </Button.Flyout>
              </Button>
              <TextBox Text="first field" />
            </StackPanel>
            """);

        Assert.Matches("<button type=\"button\" popovertarget=\"nav\" class=\"[^\"]+\">Menu</button>", html);
        Assert.Matches("<button type=\"button\" popovertarget=\"m1\" class=\"[^\"]+\" style=\"anchor-name:--m1\">Account</button><div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+\"", html);
        Assert.Matches("<a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"/profile\"><span class=\"mh\">Profile</span></a>", html);
        Assert.Contains("name=\"f0\"", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-05")]
    public async Task AccentButtonsHaveTheAccentLookAsync()
    {
        var page = await fixture.CompileAsync("""<Button Classes="accent" Content="Save" />""");

        var classes = Regex.Match(EngineFixture.Body(page), "<button[^>]* class=\"([^\"]+)\"").Groups[1].Value.Split(' ');
        var rules = string.Join('\n', page.Css.Split('\n').Where(rule => classes.Any(name => rule.StartsWith($".{name}", StringComparison.Ordinal))));
        Assert.Contains($"background:{await ColorAsync("AccentButtonBackground")};", rules, StringComparison.Ordinal);
        Assert.Contains($"color:{await ColorAsync("AccentButtonForeground")};", rules, StringComparison.Ordinal);
        Assert.Matches($":hover\\{{background:{Regex.Escape(await ColorAsync("AccentButtonBackgroundPointerOver"))}!important;", rules);
    }

    [Fact]
    [Trait("AC", "BUTTONS-124")]
    public async Task ButtonsWithAnAddressAreLinksAsync()
    {
        const string Body = """
            <StackPanel>
              <Button Content="Docs" a:Web.Href="/docs" />
              <Button Content="Profile" a:Web.Href="{Binding Name}" />
              <Button Content="Ping" Command="{Binding PingCommand}" a:Web.Href="/ping" />
              <Button Content="Centered" Height="36" a:Web.Href="/centered">
                <Button.Theme>
                  <ControlTheme TargetType="Button">
                    <Setter Property="Template">
                      <ControlTemplate>
                        <Border Name="PART_Border" Padding="16,0"><ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" /></Border>
                      </ControlTemplate>
                    </Setter>
                  </ControlTheme>
                </Button.Theme>
              </Button>
            </StackPanel>
            """;
        var model = new Profile { Name = "/people/ada" };
        var html = await fixture.HtmlAsync(Body, model);
        var page = await fixture.CompileAsync(Body, model);
        var centered = System.Text.RegularExpressions.Regex.Match(html, "<a href=\"/centered\"[^>]*class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
        var rules = page.Css.Split('\n').Where(rule => centered.Any(name => rule.StartsWith("." + name + "{", StringComparison.Ordinal))).ToList();

        Assert.Matches("<a href=\"/docs\"[^>]*class=\"[^\"]*lk[^\"]*\"[^>]*>.*Docs.*?</a>", html);
        Assert.Matches("<a href=\"/people/ada\"[^>]*>.*Profile.*?</a>", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\"[^>]*>.*Ping.*?</button>", html);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(html, "<button"));
        Assert.Contains(rules, rule => rule.Contains("display:inline-flex;align-items:center;justify-content:center;", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "BUTTONS-06")]
    public async Task RepeatButtonsSubmitTheirCommandOnceAsync()
    {
        var toggles = new Toggles();
        const string Body = """<RepeatButton Content="More" Command="{Binding RepeatCommand}" />""";

        var html = await fixture.HtmlAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("_btn", "0|"));

        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"0\\|\" class=\"[^\"]+\">More</button>", html);
        Assert.Equal(1, toggles.Repeats);
    }

    [Fact]
    [Trait("AC", "BUTTONS-07")]
    public async Task ToggleButtonsAreHiddenCheckboxesInLabelsAsync()
    {
        var toggles = new Toggles();
        const string Body = """
            <StackPanel>
              <ToggleButton Content="Bold" IsChecked="{Binding Bold}" />
              <ToggleButton Content="Italic" IsChecked="{Binding Italic}" />
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("f1", "on"));

        var html = EngineFixture.Body(page, new Toggles());
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"checkbox\" name=\"f0\" value=\"on\" checked class=\"vh\">Bold</label>", html);
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"checkbox\" name=\"f1\" value=\"on\" class=\"vh\">Italic</label>", html);
        Assert.Matches($":has\\(>input:checked\\)\\{{background:{Regex.Escape(await ColorAsync("ToggleButtonBackgroundChecked"))}!important;", page.Css);
        Assert.True(page.Reusable);
        Assert.False(toggles.Bold);
        Assert.True(toggles.Italic);
    }

    [Fact]
    [Trait("AC", "BUTTONS-08")]
    public async Task RadioButtonsInAPanelShareOneNameAsync()
    {
        var choices = new Choices();
        const string Body = """
            <StackPanel>
              <TextBox Text="before" />
              <StackPanel>
                <RadioButton Content="Small" IsChecked="{Binding Small}" />
                <RadioButton Content="Medium" IsChecked="{Binding Medium}" />
                <RadioButton Content="Large" IsChecked="{Binding Large}" />
              </StackPanel>
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body, choices);
        await fixture.PostAsync(Body, choices, ("f1", "2"));

        var html = EngineFixture.Body(page, new Choices());
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"radio\" name=\"f1\" value=\"0\">Small</label>", html);
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"radio\" name=\"f1\" value=\"1\" checked>Medium</label>", html);
        Assert.Matches("<label class=\"[^\"]+\"><input type=\"radio\" name=\"f1\" value=\"2\">Large</label>", html);
        Assert.True(page.Reusable);
        Assert.Equal((false, false, true), (choices.Small, choices.Medium, choices.Large));
    }

    [Fact]
    [Trait("AC", "BUTTONS-125")]
    public async Task RadioGroupsAreAnsweredFromThePlanAsTheEngineAnswersThemAsync()
    {
        const string Bools = """
            <StackPanel>
              <RadioButton Content="Small" IsChecked="{Binding Small}" />
              <RadioButton Content="Medium" IsChecked="{Binding Medium}" />
              <RadioButton Content="Large" IsChecked="{Binding Large}" />
            </StackPanel>
            """;
        const string Converted = """
            <StackPanel>
              <StackPanel.Resources><m:SizeIs x:Key="Is" /></StackPanel.Resources>
              <RadioButton Content="Small" IsChecked="{Binding Size, Converter={StaticResource Is}, ConverterParameter=Small}" />
              <RadioButton Content="Medium" IsChecked="{Binding Size, Converter={StaticResource Is}, ConverterParameter=Medium}" />
              <RadioButton Content="Large" IsChecked="{Binding Size, Converter={StaticResource Is}, ConverterParameter=Large}" />
            </StackPanel>
            """;
        var bools = await fixture.PlansLikeAvaloniaAsync(Bools, new Choices());
        var converted = await fixture.PlansLikeAvaloniaAsync(Converted, new SizedChoice());
        var (planned, engine) = (new Choices(), new Choices());
        var (plannedSize, engineSize) = (new SizedChoice(), new SizedChoice());

        var bound = await PlanForms.ApplyAsync(bools.Plan, [planned], Form(("_chg", "0"), ("f0", "2")), new Plans.AdapterSlotResolver());
        var sized = await PlanForms.ApplyAsync(converted.Plan, [plannedSize], Form(("_chg", "0"), ("f0", "2")), new Plans.AdapterSlotResolver());
        await fixture.PostAsync(Bools, engine, ("_chg", "0"), ("f0", "2"));
        await fixture.PostAsync(Converted, engineSize, ("_chg", "0"), ("f0", "2"));

        Assert.Equal(PostStatus.Rendered, bound);
        Assert.Equal(PostStatus.Rendered, sized);
        Assert.Equal((false, false, true), (planned.Small, planned.Medium, planned.Large));
        Assert.Equal((engine.Small, engine.Medium, engine.Large), (planned.Small, planned.Medium, planned.Large));
        Assert.Equal(Sized.Large, plannedSize.Size);
        Assert.Equal(engineSize.Size, plannedSize.Size);
        Assert.Matches("<input type=\"radio\" name=\"f0\" value=\"2\" checked>", EngineFixture.Body(converted, plannedSize));
    }

    private static System.Collections.Immutable.ImmutableDictionary<string, string> Form(params (string Key, string Value)[] fields) =>
        System.Collections.Immutable.ImmutableDictionary.CreateRange(fields.Select(field => KeyValuePair.Create(field.Key, field.Value)));

    [Fact]
    [Trait("AC", "BUTTONS-09")]
    public async Task RadioButtonGroupsSpanPanelsAsync()
    {
        var choices = new Choices();
        const string Body = """
            <StackPanel>
              <StackPanel>
                <RadioButton GroupName="size" Content="Small" IsChecked="{Binding Small}" />
                <RadioButton GroupName="color" Content="Red" IsChecked="{Binding Red}" />
              </StackPanel>
              <StackPanel>
                <RadioButton GroupName="size" Content="Large" IsChecked="{Binding Large}" />
                <RadioButton GroupName="color" Content="Blue" IsChecked="{Binding Blue}" />
              </StackPanel>
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body, choices);
        await fixture.PostAsync(Body, choices, ("f0", "1"));

        Assert.Matches("name=\"f0\" value=\"0\"[^>]*>Small</label>.*name=\"f1\" value=\"0\" checked[^>]*>Red</label>.*name=\"f0\" value=\"1\"[^>]*>Large</label>.*name=\"f1\" value=\"1\"[^>]*>Blue</label>", html);
        Assert.Equal((false, true), (choices.Small, choices.Large));
        Assert.Equal((true, false), (choices.Red, choices.Blue));
    }

    [Fact]
    [Trait("AC", "BUTTONS-10")]
    public async Task ToggleSwitchesAreSwitchCheckboxesAsync()
    {
        var toggles = new Toggles();
        const string Body = """<ToggleSwitch Content="Wi-Fi" IsChecked="{Binding Bold}" />""";

        var page = await fixture.CompileAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles);

        Assert.Matches("<label class=\"[^\"]+\"><span>Wi-Fi</span><span class=\"[^\"]+\"><input type=\"checkbox\" role=\"switch\" name=\"f0\" value=\"on\" checked><span class=\"off\">Off</span><span class=\"on\">On</span></span></label>", EngineFixture.Body(page, new Toggles()));
        Assert.Matches($"input:checked\\)>\\.tsr>input\\{{[^}}]*background-color:{Regex.Escape(await ColorAsync("ToggleSwitchFillOn"))};", page.Css);
        Assert.Matches("input:checked~\\.off,[^{]+input:not\\(:checked\\)~\\.on\\{display:none\\}", page.Css);
        Assert.False(toggles.Bold);
    }

    [Fact]
    [Trait("AC", "BUTTONS-11")]
    public async Task DropDownButtonsOpenTheirFlyoutAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <DropDownButton Content="Sort">
                <DropDownButton.Flyout>
                  <MenuFlyout>
                    <MenuItem Header="By name" a:Web.Href="?sort=name" />
                  </MenuFlyout>
                </DropDownButton.Flyout>
              </DropDownButton>
              <DropDownButton Content="Info">
                <DropDownButton.Flyout>
                  <Flyout><TextBlock Text="Hello" /></Flyout>
                </DropDownButton.Flyout>
              </DropDownButton>
              <TextBox Text="first field" />
            </StackPanel>
            """);

        Assert.Matches("<button type=\"button\" popovertarget=\"m1\" class=\"[^\"]+\" style=\"anchor-name:--m1\">Sort<svg[^>]*><path d=\"M1939 486L2029 576L1024 1581L19 576L109 486L1024 1401L1939 486Z\"/></svg></button><div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+\"", html);
        Assert.Matches("<a role=\"menuitem\" tabindex=\"-1\" class=\"mi [^\"]+\" href=\"\\?sort=name\"><span class=\"mh\">By name</span></a>", html);
        Assert.Matches("<button type=\"button\" popovertarget=\"m2\"[^>]*>Info<svg.*?</button><div popover id=\"m2\" role=\"dialog\" class=\"fly [^\"]+\" style=\"[^\"]+\"><div[^>]*>Hello</div></div>", html);
        Assert.Contains("name=\"f0\"", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-12")]
    public async Task SplitButtonsSubmitAndOpenTheirFlyoutAsync()
    {
        var toggles = new Toggles();
        const string Body = """
            <StackPanel>
              <SplitButton Content="Send" Command="{Binding SendCommand}">
                <SplitButton.Flyout>
                  <MenuFlyout><MenuItem Header="Schedule" a:Web.Href="/schedule" /></MenuFlyout>
                </SplitButton.Flyout>
              </SplitButton>
              <SplitButton Content="Off" IsEnabled="False" />
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("_btn", "0|"));

        Assert.Matches("<div class=\"[^\"]+\" style=\"anchor-name:--m1\"><button type=\"submit\" name=\"_btn\" value=\"0\\|\" class=\"[^\"]+\">Send</button><span class=\"[^\"]+\"></span><button type=\"button\" popovertarget=\"m1\" class=\"[^\"]+\"><svg[^>]*><path d=\"M1939 [^\"]+\"/></svg></button></div><div popover id=\"m1\" role=\"menu\" data-aioros-overlay class=\"menu k[0-9a-f]+\"", html);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"1\\|\" class=\"[^\"]+\" disabled>Off</button><span class=\"[^\"]+\"></span><button type=\"button\" class=\"[^\"]+\" disabled><svg", html);
        Assert.Equal(1, toggles.Sends);
    }

    [Fact]
    [Trait("AC", "BUTTONS-13")]
    public async Task ToggleSplitButtonsToggleAndOpenTheirFlyoutAsync()
    {
        var toggles = new Toggles();
        const string Body = """
            <ToggleSplitButton Content="Bold" IsChecked="{Binding Bold}">
              <ToggleSplitButton.Flyout>
                <MenuFlyout><MenuItem Header="Heavy" a:Web.Href="/heavy" /></MenuFlyout>
              </ToggleSplitButton.Flyout>
            </ToggleSplitButton>
            """;

        var page = await fixture.CompileAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles);

        Assert.Matches("<div class=\"[^\"]+\" style=\"anchor-name:--m1\"><label class=\"[^\"]+\"><input type=\"checkbox\" name=\"f0\" value=\"on\" checked class=\"vh\">Bold</label><span class=\"[^\"]+\"></span><button type=\"button\" popovertarget=\"m1\"", EngineFixture.Body(page, new Toggles()));
        Assert.Matches($":has\\(>input:checked\\)\\{{background:{Regex.Escape(await ColorAsync("SplitButtonBackgroundChecked"))};", page.Css);
        Assert.False(toggles.Bold);
    }

    [Fact]
    [Trait("AC", "BUTTONS-14")]
    public async Task ButtonSpinnersPostTheirDirectionAsync()
    {
        var toggles = new Toggles();
        const string Body = """
            <StackPanel>
              <m:SpinProbe Content="7" ValidSpinDirection="Increase" />
              <ButtonSpinner Content="Left" ButtonSpinnerLocation="Left" AllowSpin="False" />
              <ButtonSpinner Content="Hidden" ShowButtonSpinner="False" />
            </StackPanel>
            """;

        var html = await fixture.HtmlAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("f0", "increase"));
        await fixture.PostAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("f1", "decrease"));

        Assert.Matches("<div class=\"[^\"]+\"><div class=\"[^\"]+\">7</div><button type=\"submit\" name=\"f0\" value=\"increase\" class=\"[^\"]+\"><svg[^>]*>.*?</svg></button><button type=\"submit\" name=\"f1\" value=\"decrease\" class=\"[^\"]+\" disabled><svg", html);
        Assert.Matches("<div class=\"[^\"]+\"><button type=\"submit\" name=\"f2\" value=\"increase\" class=\"[^\"]+\" disabled><svg.*?</button><button type=\"submit\" name=\"f3\" value=\"decrease\" class=\"[^\"]+\" disabled><svg.*?</button><div class=\"[^\"]+\">Left</div></div>", html);
        Assert.Matches("<div class=\"[^\"]+\"><div class=\"[^\"]+\">Hidden</div></div>", html);
        Assert.Equal([SpinDirection.Increase], toggles.Spins);
    }

    [Fact]
    [Trait("AC", "BUTTONS-15")]
    public async Task ThreeStateCheckBoxesCycleThroughThreeRadiosAsync()
    {
        var toggles = new Toggles();
        const string Body = """<CheckBox IsThreeState="True" Content="All" IsChecked="{Binding Mixed}" />""";

        var page = await fixture.CompileAsync(Body, toggles);
        await fixture.PostAsync(Body, toggles, ("f0", "null"));

        var html = EngineFixture.Body(page, new Toggles());
        Assert.Matches("<div class=\"tri [^\"]+\"><label class=\"u3 (mk[0-9a-f]+ k[0-9a-f]+)\"><input type=\"radio\" name=\"f0\" value=\"true\" checked class=\"vh\"><span class=\"box\"></span>All</label><label class=\"c3 \\1\"><input type=\"radio\" name=\"f0\" value=\"null\" class=\"vh\"><span class=\"box\"></span>All</label><label class=\"i3 \\1\"><input type=\"radio\" name=\"f0\" value=\"false\" class=\"vh\"><span class=\"box\"></span>All</label></div>", html);
        Assert.Matches("\\.mk[0-9a-f]+\\.c3>\\.box\\{[^}]*background-image:url\\(", page.Css);
        Assert.Matches("\\.mk[0-9a-f]+\\.i3>\\.box\\{[^}]*background-image:url\\(", page.Css);
        Assert.Contains(":has(input[value=false]:checked)>.u3", page.Css, StringComparison.Ordinal);
        Assert.True(page.Reusable);
        Assert.Null(toggles.Mixed);
    }

    private async Task<string> ColorAsync(string key) =>
        await fixture.Engine.DispatchAsync(
            () => Application.Current!.TryFindResource(key, ThemeVariant.Light, out var value)
                ? value switch
                {
                    IBrush brush => CssValues.Brush(brush) ?? "transparent",
                    Color color => CssValues.Color(color),
                    _ => throw new InvalidOperationException(key),
                }
                : throw new InvalidOperationException(key),
            TestContext.Current.CancellationToken);

    [Fact]
    [Trait("AC", "BUTTONS-16")]
    public async Task ButtonsWithBoundTextContentArePlannedAsync()
    {
        var page = await fixture.CompileAsync("""<Button Content="{Binding Title}" />""", new Article("First"));
        var html = EngineFixture.Body(page, new Article("Fish & chips"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches(">Fish &amp; chips</button>", html);
        Assert.DoesNotContain("First", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-17")]
    public async Task ButtonsKeepTheirOwnPaddingAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Button Content="Small" Padding="4" Background="#ff0000" />
              <Button Content="Large" Padding="20,10" Background="#0000ff" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rule(string text) => string.Concat(Regex.Match(html, "class=\"([^\"]+)\"[^>]*>" + text + "</button>").Groups[1].Value
            .Split(' ').Select(name => Regex.Match(page.Css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

        Assert.Contains("padding:" + BoxStyles.Edges(new Thickness(4)) + ";", Rule("Small"), StringComparison.Ordinal);
        Assert.Contains("background:#ff0000;", Rule("Small"), StringComparison.Ordinal);
        Assert.Contains("padding:" + BoxStyles.Edges(new Thickness(20, 10)) + ";", Rule("Large"), StringComparison.Ordinal);
        Assert.Contains("background:#0000ff;", Rule("Large"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-12")]
    public async Task CommandsThePlanCannotFollowAreLeftToTheEngineAsync()
    {
        var editor = new Editor();
        var page = await fixture.CompileAsync("""
            <ItemsControl ItemsSource="{Binding Commands}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="x:String">
                  <Button Content="{Binding}" Command="{ReflectionBinding $parent[ListBox].DataContext.OpenCommand}" CommandParameter="{Binding}" />
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            """, editor);
        var status = await Runtime.Rendering.PlanForms.ApplyAsync(page.Plan, [editor], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("_btn", "0|Open"), new Plans.AdapterSlotResolver());

        Assert.Null(status);
    }

    [Fact]
    [Trait("AC", "BUTTONS-18")]
    public async Task CheckBoxesHaveFluentsBoxAndHeightAsync()
    {
        const string Body = """
            <StackPanel>
              <CheckBox Content="Agree" />
              <CheckBox Content="Tall" MinHeight="40" />
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;

        var html = EngineFixture.Body(page);
        Assert.Equal(2, html.Split("<input type=\"checkbox\"").Length - 1);
        Assert.Matches(">input\\{appearance:none;[^}]*width:20px;height:20px;", page.Css);
        Assert.Matches("\\{[^}]*display:flex;flex-direction:row;align-items:center;gap:8px;min-height:32px;", page.Css);
        Assert.Matches("\\{[^}]*min-height:40px;", page.Css);
        Assert.Equal([28.0, 28.0], laid.Select(text => text.X));
        Assert.Equal([8.0, 32 + 12.0], laid.Select(text => text.Y));
    }

    [Fact]
    [Trait("AC", "BUTTONS-19")]
    public async Task DisabledCheckBoxesLookDisabledAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <CheckBox Content="Off" IsEnabled="False" />
              <CheckBox Content="On" IsEnabled="False" IsChecked="True" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<input type=\"checkbox\" name=\"f0\"(?: data-aioros-local=\"\")? value=\"on\" disabled>Off</label>", html);
        Assert.Matches("<input type=\"checkbox\" name=\"f1\"(?: data-aioros-local=\"\")? value=\"on\" checked disabled>On</label>", html);
        Assert.Matches("color:#000000;display:flex;[^}]*color:rgba\\(0,0,0,0\\.4\\);", page.Css);
    }

    [Fact]
    [Trait("AC", "BUTTONS-20")]
    public async Task HyperlinkButtonsHaveTheirStateLooksAsync()
    {
        var page = await fixture.CompileAsync("""<HyperlinkButton NavigateUri="/go" Content="Go" />""", theme: "dark");

        var classes = Regex.Match(EngineFixture.Body(page), "<a href=\"/go\" class=\"lk (s[0-9a-f]+) k[0-9a-f]+\">Go</a>");
        Assert.True(classes.Success);
        Assert.Matches($"\\.{classes.Groups[1].Value}:not\\(:disabled\\):hover\\{{background:rgba\\(255,255,255,0.059\\)!important;[^}}]*\\}}", page.Css);
        Assert.Matches($"\\.{classes.Groups[1].Value}:not\\(:disabled\\):active\\{{background:rgba\\(255,255,255,0.039\\)!important;[^}}]*color:#[0-9a-f]{{6}}!important;[^}}]*\\}}", page.Css);
    }

    [Fact]
    [Trait("AC", "BUTTONS-21")]
    public async Task UnboundContentTakesItsTypedTemplateAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.DataTemplates>
                <DataTemplate DataType="Geometry"><PathIcon Data="{Binding}" /></DataTemplate>
              </StackPanel.DataTemplates>
              <Button Command="{Binding PingCommand}"><StreamGeometry>M0,0 L10,0 L10,10 Z</StreamGeometry></Button>
              <Button Command="{Binding PingCommand}" Content="Plain" />
            </StackPanel>
            """, new Profile());

        var html = EngineFixture.Body(page, new Profile { Name = "Grace" });

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("<button type=\"submit\"[^>]*><svg[^>]*viewBox=\"0 0 10 10\"[^>]*><path d=\"M0 0L10 0L10 10L0 0Z\"", html);
        Assert.DoesNotContain("StreamGeometry", html, StringComparison.Ordinal);
        Assert.Matches("<button type=\"submit\"[^>]*>Plain</button>", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-42")]
    public async Task RadioButtonsCheckedThroughAConverterAsync()
    {
        const string Body = """
            <StackPanel>
              <RadioButton Content="Low" IsChecked="{Binding Priority, Mode=OneWay, Converter={x:Static m:LevelMatch.Instance}, ConverterParameter={x:Static m:Level.Low}}" />
              <RadioButton Content="High" IsChecked="{Binding Priority, Mode=OneWay, Converter={x:Static m:LevelMatch.Instance}, ConverterParameter={x:Static m:Level.High}}" />
            </StackPanel>
            """;
        var low = new Ticket("low", 1, null);
        var high = new Ticket("high", 3, null);
        var planned = await fixture.CompileAsync(Body, low);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        Assert.True(planned.Reusable, planned.Failure);
        foreach (var ticket in new[] { low, high })
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(Body, ticket), ticket);
            Assert.Equal(expected, EngineFixture.Body(planned, ticket));
            Assert.Equal(expected, EngineFixture.Body(saved, ticket));
        }

        Assert.Matches("value=\"1\" checked[^>]*>High<", EngineFixture.Body(saved, high));
    }

    [Fact]
    [Trait("AC", "BUTTONS-77")]
    public async Task ButtonsTakeTheThemeAStyleSetsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Orientation="Horizontal">
              <StackPanel.Resources>
                <ControlTheme x:Key="Flat" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}"><Setter Property="FontWeight" Value="Light" /></ControlTheme>
              </StackPanel.Resources>
              <StackPanel.Styles><Style Selector="Button"><Setter Property="Theme" Value="{DynamicResource Flat}" /></Style></StackPanel.Styles>
              <Button Content="One" />
              <Button Content="Two" Classes="accent" />
              <m:Marked>
                <m:Marked.Styles><Style Selector="Button:bare"><Setter Property="MinWidth" Value="40" /></Style></m:Marked.Styles>
              </m:Marked>
            </StackPanel>
            """);

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("font-weight:300;", page.Css, StringComparison.Ordinal);
        Assert.Contains("min-width:40px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-92")]
    public async Task BoundLinkAddressesArePlannedPerItemAsync()
    {
        const string Body = """
            <StackPanel>
              <HyperlinkButton Content="Home" a:Web.Href="/" />
              <ItemsControl ItemsSource="{Binding Crumbs}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="m:Crumb">
                    <StackPanel Orientation="Horizontal">
                      <HyperlinkButton Content="{Binding Name}" a:Web.Href="{Binding Href}" />
                      <Border a:Web.Href="{Binding Href}"><TextBlock Text="{Binding Name}" /></Border>
                    </StackPanel>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """;
        var models = new[] { new Trail(new Crumb("Docs", "/docs"), new Crumb("B", "/docs/b")), new Trail(new Crumb("Q", "/search?q=a&b=c")) };

        var page = await fixture.PlansLikeAvaloniaAsync(Body, models);

        var html = EngineFixture.BodyAt(page, "/docs/b", models[0]);
        Assert.Matches("<a href=\"/\" data-nav class=\"[^\"]*\">Home</a>", html);
        Assert.Matches("<a href=\"/docs\" aria-current=\"page\" data-nav class=\"[^\"]*\">Docs</a>", html);
        Assert.Matches("<a href=\"/docs/b\" aria-current=\"page\" data-nav class=\"[^\"]*\">B</a>", html);
        Assert.Matches("<a href=\"/docs/b\" aria-current=\"page\" data-nav class=\"[^\"]*\"><div[^>]*>B</div></a>", html);
        Assert.Matches("<a href=\"/search\\?q=a&amp;b=c\" data-nav class=\"[^\"]*\">Q</a>", EngineFixture.BodyAt(page, "/docs/b", models[1]));
    }

    [Fact]
    [Trait("AC", "BUTTONS-96")]
    public async Task HotKeysAreWrittenAsShortcutsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Button HotKey="Ctrl+S" Content="Save" />
              <Button HotKey="Ctrl+K" Content="Search"><Button.Flyout><Flyout><TextBlock Text="Find" /></Flyout></Button.Flyout></Button>
              <Button HotKey="Shift+F1" Content="Help" />
              <Button Content="Plain" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.True(page.Reusable, page.Failure);
        Assert.DoesNotMatch("<button [^>]*hidden", html);
        Assert.Matches("<button type=\"submit\"[^>]* data-aioros-key=\"ctrl\\+KeyS\" aria-keyshortcuts=\"Control\\+S\"[^>]*>Save</button>", html);
        Assert.Matches("<button type=\"button\" popovertarget=\"[^\"]+\"[^>]* data-aioros-key=\"ctrl\\+KeyK\" aria-keyshortcuts=\"Control\\+K\"[^>]*>Search</button>", html);
        Assert.Matches("<button [^>]* data-aioros-key=\"shift\\+F1\" aria-keyshortcuts=\"Shift\\+F1\"[^>]*>Help</button>", html);
        Assert.Matches("<button (?![^>]*data-aioros-key)[^>]*>Plain</button>", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-116")]
    public async Task ChoiceHotKeysAreWrittenAsShortcutsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Card" TargetType="RadioButton">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Card" BorderBrush="#e5e5e5" BorderThickness="1" Padding="12">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                          <Border Name="PART_Indicator" Width="16" Height="16" CornerRadius="8" Background="#e5e5e5" />
                          <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <RadioButton HotKey="B" Content="Bee" />
              <RadioButton Theme="{StaticResource Card}" HotKey="C" Content="Sea" />
              <CheckBox HotKey="Ctrl+D" Content="Dee" />
              <CheckBox Content="Plain" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page, [null]);

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("<input type=\"radio\"[^>]* data-aioros-key=\"KeyB\" aria-keyshortcuts=\"B\"[^>]*>Bee", html);
        Assert.Matches("<input type=\"radio\"[^>]* data-aioros-key=\"KeyC\" aria-keyshortcuts=\"C\"[^>]*>", html);
        Assert.Matches("<input type=\"checkbox\"[^>]* data-aioros-key=\"ctrl\\+KeyD\" aria-keyshortcuts=\"Control\\+D\"[^>]*>Dee", html);
        Assert.Matches("<input type=\"checkbox\"(?![^>]*data-aioros-key)[^>]*>Plain", html);
    }

    [Fact]
    [Trait("AC", "BUTTONS-112")]
    public async Task LinksWriteTheirControlContentAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <HyperlinkButton NavigateUri="/docs"><TextBlock Text="Read the docs" TextWrapping="Wrap" /></HyperlinkButton>
              <HyperlinkButton NavigateUri="/home" Content="Home" />
            </StackPanel>
            """);

        Assert.DoesNotContain("Avalonia.Controls.TextBlock", html, StringComparison.Ordinal);
        Assert.Matches("<a href=\"/docs\"[^>]*><div class=\"k[0-9a-f]+\">Read the docs</div></a>", html);
        Assert.Matches("<a href=\"/home\"[^>]*>Home</a>", html);
    }
    [Fact]
    [Trait("AC", "BUTTONS-121")]
    public async Task AButtonWaitingForAnAnswerIsMarkedAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel a:Web.Answers="True">
              <RadioButton GroupName="g" Content="A" />
              <Button a:Web.NeedsAnswer="True" IsEnabled="False" Content="Next" />
              <Button Content="Back" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);

        Assert.Matches("<div[^>]* data-aioros-answers[ >]", html);
        Assert.Matches("<button[^>]* data-aioros-needs-answer[^>]*>(<[^>]+>)*Next<", html);
        Assert.DoesNotMatch("<button[^>]* data-aioros-needs-answer[^>]*>(<[^>]+>)*Back<", html);
    }
    [Fact]
    [Trait("AC", "BUTTONS-122")]
    public async Task AKeyScopesButtonsAreMarkedWithTheirKeysAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel a:Web.KeyScope="True">
              <Button a:Web.Keys="Ctrl+Enter Right" Content="Next" />
              <Button a:Web.Keys="Nope Left" Content="Previous" />
              <Button Content="Reset" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);

        Assert.Matches("<div[^>]* data-aioros-keyscope[ >]", html);
        Assert.Matches("<button[^>]* data-aioros-keys=\"ctrl\\+Enter ArrowRight\"[^>]*>(<[^>]+>)*Next<", html);
        Assert.Matches("<button[^>]* data-aioros-keys=\"ArrowLeft\"[^>]*>(<[^>]+>)*Previous<", html);
        Assert.DoesNotMatch("<button[^>]* data-aioros-keys[^>]*>(<[^>]+>)*Reset<", html);
        Assert.Contains("[data-aioros-keys]", System.Text.Encoding.UTF8.GetString(Aioros.Targets.Ssr.Documents.Scripts.Bytes.Span), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-126")]
    public async Task ALinkPlacesItsContentAsItsAlignmentSaysAsync()
    {
        const string Body = """
            <StackPanel Width="300" HorizontalAlignment="Left">
              <HyperlinkButton HorizontalAlignment="Stretch" HorizontalContentAlignment="Center" NavigateUri="/a" Content="Centered" />
              <HyperlinkButton HorizontalAlignment="Stretch" HorizontalContentAlignment="Right" NavigateUri="/b" Content="Right" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body);

        var web = await BrowserLayout.TextsAsync(page, 500, 300);
        var desktop = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 500, 300))).Texts;

        foreach (var text in new[] { "Centered", "Right" })
        {
            Assert.InRange(web.Single(found => found.Text == text).X, desktop.Single(found => found.Text == text).X - 3, desktop.Single(found => found.Text == text).X + 3);
        }
    }
}
