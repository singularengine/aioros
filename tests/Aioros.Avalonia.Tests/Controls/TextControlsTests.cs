using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class TextControlsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "TEXT-01")]
    public async Task TextBlocksAreTextElementsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Classes="h1" FontSize="30" FontWeight="Bold" Text="Title &amp; more" />
              <TextBlock Classes="h2" Text="Section" />
              <TextBlock TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" TextAlignment="Center" Text="Body" />
              <TextBlock TextWrapping="Wrap" Text="line one&#10;  line two" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<h1 class=\"k[0-9a-f]+\">Title &amp; more</h1>", html);
        Assert.Matches("<h2 class=\"k[0-9a-f]+\">Section</h2>", html);
        Assert.Matches("<div class=\"k[0-9a-f]+\">Body</div>", html);
        Assert.Contains("font-size:30px;font-weight:700;", page.Css, StringComparison.Ordinal);
        Assert.Contains("white-space:pre;overflow:hidden;text-overflow:ellipsis;text-align:center;", page.Css, StringComparison.Ordinal);
        Assert.Contains("white-space:pre-wrap;overflow-wrap:anywhere;", page.Css, StringComparison.Ordinal);
        Assert.Contains(">line one\n  line two</div>", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-02")]
    public async Task TextBoxesAreInputsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <TextBox Text="{Binding Name}" PlaceholderText="Name" />
              <TextBox AcceptsReturn="True" Text="Line" />
              <TextBox a:Web.InputType="email" Text="a@b.c" />
            </StackPanel>
            """, new Profile());

        Assert.Matches("<input type=\"text\" name=\"f0\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" placeholder=\"Name\" value=\"Ada\">", html);
        Assert.Matches("<textarea name=\"f1\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" wrap=\"off\">Line</textarea>", html);
        Assert.Matches("<input type=\"email\" name=\"f2\"(?: data-aioros-local=\"\")?", html);
    }

    [Fact]
    [Trait("AC", "TEXT-03")]
    public async Task PostedTextReachesTheModelAsync()
    {
        var posted = new Profile();
        var omitted = new Profile();
        const string Body = """<TextBox Text="{Binding Name}" />""";

        await fixture.PostAsync(Body, posted, ("f0", "Grace"));
        await fixture.PostAsync(Body, omitted);

        Assert.Equal("Grace", posted.Name);
        Assert.Equal("Ada", omitted.Name);
    }

    [Fact]
    [Trait("AC", "TEXT-04")]
    public async Task InlinesAreWrittenInOrderAsync()
    {
        const string Body = """
            <TextBlock><Run Text="Hi " /><Bold>bold</Bold><Italic>it</Italic><Underline>under</Underline><Span FontSize="20" Foreground="Red">big</Span><LineBreak /><Run Text="{Binding Customer}" /><InlineUIContainer><Border Width="12" Height="12" Background="Red" /></InlineUIContainer></TextBlock>
            """;
        var page = await fixture.CompileAsync(Body, new Order());

        Assert.Null(page.Failure);
        Assert.Matches(
            "<div class=\"k[0-9a-f]+\">Hi <b>bold</b><i>it</i><u>under</u><span class=\"k[0-9a-f]+\">big</span><br>Grace<span class=\"k[0-9a-f]+\"><div class=\"c k[0-9a-f]+\"></div></span></div>",
            EngineFixture.Body(page, new Order { Customer = "Grace" }));
        Assert.Contains("font-size:20px;color:#ff0000;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-05")]
    public async Task SelectableTextIsSelectableAsync()
    {
        var page = await fixture.CompileAsync("""<SelectableTextBlock Text="Pick me" />""");

        Assert.Matches("<div class=\"sl[0-9a-f]+ k[0-9a-f]+\">Pick me</div>", EngineFixture.Body(page));
        Assert.Contains("user-select:text;cursor:text;", page.Css, StringComparison.Ordinal);
        Assert.Matches(@"\.sl[0-9a-f]+::selection\{background:#0078d7\}", page.Css);
    }

    [Fact]
    [Trait("AC", "TEXT-06")]
    public async Task AccessKeyMarkersAreRemovedAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <AccessText Text="_Save" />
              <AccessText Text="Fish __ Chips" />
            </StackPanel>
            """);

        Assert.Matches("<div class=\"k[0-9a-f]+\">Save</div>", html);
        Assert.Matches("<div class=\"k[0-9a-f]+\">Fish _ Chips</div>", html);
    }

    [Fact]
    [Trait("AC", "TEXT-07")]
    public async Task LabelsPointAtTheirTargetAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <Label Content="_Email" Target="{Binding #box}" />
              <TextBox Name="box" a:Web.Id="email" Text="a@b.c" />
              <Label Content="Plain" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        Assert.Matches("<label for=\"email\" accesskey=\"e\" class=\"k[0-9a-f]+\">Email</label>", html);
        Assert.Matches("<input type=\"text\" id=\"email\" name=\"f0\"", html);
        Assert.Matches("<label class=\"k[0-9a-f]+\">Plain</label>", html);
        Assert.Contains("padding:3px 3px 3px 3px;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-08")]
    public async Task TextBoxVariantsAreInputAttributesAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <TextBox PasswordChar="*" Text="secret" />
              <TextBox MaxLength="5" IsReadOnly="True" Watermark="Code" />
              <TextBox IsEnabled="False" Text="off" />
              <TextBox AcceptsReturn="True" TextWrapping="Wrap" Text="Wrapped" />
            </StackPanel>
            """);

        Assert.Matches("<input type=\"password\" name=\"f0\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" value=\"secret\">", html);
        Assert.Matches("<input type=\"text\" name=\"f1\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" placeholder=\"Code\" maxlength=\"5\" readonly value=\"\">", html);
        Assert.Matches("<input type=\"text\" name=\"f2\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" disabled value=\"off\">", html);
        Assert.Matches("<textarea name=\"f3\"(?: data-aioros-local=\"\")? class=\"[^\"]+\">Wrapped</textarea>", html);
    }

    [Fact]
    [Trait("AC", "TEXT-09")]
    public async Task TextBoxesLookLikeFluentAsync()
    {
        var page = await fixture.CompileAsync("""<TextBox Text="x" PlaceholderText="Name" />""");

        Assert.Matches("<input type=\"text\" name=\"f0\"(?: data-aioros-local=\"\")? class=\"fs s[0-9a-f]+ ph[0-9a-f]+ k[0-9a-f]+\"", EngineFixture.Body(page));
        Assert.Contains("background:rgba(255,255,255,0.4);border-color:rgba(0,0,0,0.6);border-width:1px 1px 1px 1px;border-style:solid;border-radius:3px 3px 3px 3px;padding:6px 6px 5px 10px;", page.Css, StringComparison.Ordinal);
        Assert.Contains("min-height:32px;", page.Css, StringComparison.Ordinal);
        Assert.Matches(@":hover\{background:rgba\(255,255,255,0.6\)!important;border-color:rgba\(0,0,0,0.8\)!important;", page.Css);
        Assert.Matches(@":focus\{background:#ffffff!important;border-color:#0078d7!important;box-shadow:inset 0 0 0 1px #0078d7!important;", page.Css);
        Assert.Matches(@"\.ph[0-9a-f]+::placeholder\{color:rgba\(0,0,0,0.3\)\}", page.Css);
    }

    [Fact]
    [Trait("AC", "TEXT-10")]
    public async Task MaskedTextBoxesArePatternInputsAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <MaskedTextBox Mask="(000) 000-0000" Text="{Binding Phone}" />
              <MaskedTextBox Mask="LL-90" Text="AB-12" />
            </StackPanel>
            """, new Order());

        Assert.Contains(
            @"placeholder=""(___) ___-____"" pattern=""\([0-9][0-9][0-9]\) [0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]"" value="""">",
            html,
            StringComparison.Ordinal);
        Assert.Contains(@"placeholder=""__-__"" pattern=""\p{L}\p{L}-[0-9 ]?[0-9]"" value=""AB-12"">", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-11")]
    public async Task PostedMaskedTextGoesThroughTheMaskAsync()
    {
        var posted = new Order();
        var omitted = new Order { Phone = "(555) 000-1111" };
        const string Body = """<MaskedTextBox Mask="(000) 000-0000" Text="{Binding Phone}" />""";

        await fixture.PostAsync(Body, posted, ("f0", "5551234567"));
        await fixture.PostAsync(Body, omitted);

        Assert.Equal("(555) 123-4567", posted.Phone);
        Assert.Equal("(555) 000-1111", omitted.Phone);
    }

    [Fact]
    [Trait("AC", "TEXT-12")]
    public async Task NumericUpDownsAreNumberInputsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <NumericUpDown Value="{Binding Quantity}" Minimum="0" Maximum="10" Increment="0.5" />
              <NumericUpDown Value="3" IsReadOnly="True" ShowButtonSpinner="False" />
            </StackPanel>
            """, new Order());

        var html = EngineFixture.Body(page, new Order { Quantity = 7.25m });
        Assert.Null(page.Failure);
        Assert.Matches("<input type=\"number\" name=\"f0\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" min=\"0\" max=\"10\" step=\"any\" value=\"7.25\">", html);
        Assert.Matches("<input type=\"number\" name=\"f1\"(?: data-aioros-local=\"\")? class=\"nsp [^\"]+\" step=\"any\" readonly value=\"3\">", html);
        Assert.Contains("border-radius:3px 3px 3px 3px;padding:6px 6px 5px 10px;", page.Css, StringComparison.Ordinal);
        Assert.Contains(".nsp::-webkit-inner-spin-button", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-13")]
    public async Task PostedNumbersReachTheValueAsync()
    {
        const string Body = """<NumericUpDown Value="{Binding Quantity}" Minimum="0" Maximum="10" />""";
        const string Clipping = """<NumericUpDown Value="{Binding Quantity}" Minimum="0" Maximum="10" ClipValueToMinMax="True" />""";
        var number = new Order();
        var rejected = new Order();
        var clamped = new Order();
        var cleared = new Order();
        var invalid = new Order();
        var omitted = new Order();

        await fixture.PostAsync(Body, number, ("f0", "7.5"));
        await fixture.PostAsync(Body, rejected, ("f0", "99"));
        await fixture.PostAsync(Clipping, clamped, ("f0", "99"));
        await fixture.PostAsync(Body, cleared, ("f0", string.Empty));
        await fixture.PostAsync(Body, invalid, ("f0", "seven"));
        await fixture.PostAsync(Body, omitted);

        Assert.Equal(7.5m, number.Quantity);
        Assert.Equal(2.5m, rejected.Quantity);
        Assert.Equal(10m, clamped.Quantity);
        Assert.Null(cleared.Quantity);
        Assert.Equal(2.5m, invalid.Quantity);
        Assert.Equal(2.5m, omitted.Quantity);
    }

    [Fact]
    [Trait("AC", "TEXT-14")]
    public async Task AutoCompleteBoxesAreDatalistInputsAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <AutoCompleteBox Text="{Binding Fruit}" ItemsSource="{Binding Fruits}" PlaceholderText="Fruit" />
              <AutoCompleteBox ItemsSource="{Binding Fruits}" />
            </StackPanel>
            """, new Order());

        var html = EngineFixture.Body(page, new Order { Fruit = "Pear" });
        Assert.Null(page.Failure);
        Assert.Matches("<input type=\"text\" name=\"f0\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" list=\"(dl[0-9a-f]+)\" placeholder=\"Fruit\" value=\"Pear\"><datalist id=\"\\1\"><option value=\"Apple\"><option value=\"Banana\"></datalist>", html);
        Assert.Matches("<input type=\"text\" name=\"f1\"(?: data-aioros-local=\"\")? class=\"[^\"]+\" list=\"dl[0-9a-f]+\" value=\"\">(?!<datalist)", html);
    }

    [Fact]
    [Trait("AC", "TEXT-32")]
    public async Task AutoCompleteBoxesHideTheBrowsersDropDownIndicatorAsync()
    {
        var page = await fixture.CompileAsync("""<AutoCompleteBox ItemsSource="{Binding Fruits}" />""", new Order());

        Assert.Null(page.Failure);
        Assert.Contains("input[list]::-webkit-calendar-picker-indicator{display:none!important}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-15")]
    public async Task PostedAutoCompleteTextReachesTheModelAsync()
    {
        var posted = new Order();
        var omitted = new Order();
        const string Body = """<AutoCompleteBox Text="{Binding Fruit}" ItemsSource="{Binding Fruits}" />""";

        await fixture.PostAsync(Body, posted, ("f0", "Cherry"));
        await fixture.PostAsync(Body, omitted);

        Assert.Equal("Cherry", posted.Fruit);
        Assert.Equal("Apple", omitted.Fruit);
    }

    [Fact]
    [Trait("AC", "TEXT-16")]
    public async Task LinesAreAsTallAsAvaloniaLaysThemOutAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBlock FontSize="11" Text="Eleven" />
              <TextBlock FontSize="14" Text="Fourteen" />
              <TextBlock FontSize="18" Text="Eighteen" />
              <TextBlock FontSize="20" Text="Twenty" />
              <TextBlock FontSize="28" Text="Twenty-eight" />
            </StackPanel>
            """;
        double[] sizes = [11, 14, 18, 20, 28];

        var page = await fixture.CompileAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 400, 300))).Texts;

        var ratio = double.Parse(Regex.Match(page.Css, @"\*\{line-height:round\(up,([0-9.]+)em,1px\)\}").Groups[1].Value, CultureInfo.InvariantCulture);
        var lines = sizes.Select(size => Math.Ceiling(size * ratio)).ToArray();
        Assert.Equal([14, 17, 22, 25, 34], lines);
        Assert.Equal(lines, laid.Select(text => Math.Ceiling(text.Height)));
        Assert.Equal(laid.Select(text => text.Y).Skip(1), laid.Zip(lines, (text, line) => text.Y + line).SkipLast(1));
    }

    [Fact]
    [Trait("AC", "TEXT-17")]
    public async Task TextBlockPaddingInsetsTheTextAsync()
    {
        const string Body = """
            <StackPanel>
              <Border HorizontalAlignment="Left"><TextBlock Padding="10,4" Text="Padded" /></Border>
              <TextBlock Text="After" />
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body);
        var laid = (await fixture.CaptureInAsync(Body, new CaptureOptions("light", 300, 200))).Texts;

        Assert.Contains("white-space:pre;padding:4px 10px 4px 10px;", page.Css, StringComparison.Ordinal);
        Assert.Equal([(10.0, 4.0), (0.0, 4 + 17 + 4.0)], laid.Select(text => (text.X, text.Y)));
    }

    [Fact]
    [Trait("AC", "TEXT-18")]
    public async Task ColorPickersAreColorInputsAsync()
    {
        const string Body = """<ColorPicker Color="{Binding Accent}" />""";
        var page = await fixture.CompileAsync(Body, new Palette());
        var html = EngineFixture.Body(page, new Palette());
        var other = EngineFixture.Body(page, new Palette { Accent = global::Avalonia.Media.Color.FromRgb(0xff, 0x88, 0x00) });
        var posted = new Palette { Accent = global::Avalonia.Media.Color.FromArgb(0x80, 0, 0, 0) };
        await fixture.PostAsync(Body, posted, ("f0", "#0a0B0c"));
        var refused = new Palette();
        await fixture.PostAsync(Body, refused, ("f0", "blue"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("^<input type=\"color\" name=\"f0\" class=\"[^\"]+\" value=\"#336699\">$", html);
        Assert.Contains("value=\"#ff8800\"", other, StringComparison.Ordinal);
        Assert.True(Regex.IsMatch(page.Css, "\\{[^}]*width:[0-9.]+px;height:[0-9.]+px;"), page.Css);
        Assert.Equal(global::Avalonia.Media.Color.FromArgb(0x80, 0x0a, 0x0b, 0x0c), posted.Accent);
        Assert.Equal(new Palette().Accent, refused.Accent);
    }

    [Fact]
    [Trait("AC", "TEXT-28")]
    public async Task LabelsShowTheirBoundTextAsync()
    {
        var planned = await fixture.PlansLikeAvaloniaAsync(
            """<StackPanel><Label Content="{Binding Phone}" /><Label Content="{Binding Customer}" /></StackPanel>""",
            new Order { Phone = "555" }, new Order { Phone = null }, new Order { Phone = "_Call", Customer = "a__b" });
        var targeted = await fixture.PlansLikeAvaloniaAsync(
            """<StackPanel><Label Content="{Binding Phone}" Target="{Binding #box}" /><TextBox Name="box" a:Web.Id="phone" /></StackPanel>""",
            new Order { Phone = "_Call" }, new Order { Phone = "555" });

        Assert.Matches(">Call</label><label class=\"k[0-9a-f]+\">a_b</label></div>$", EngineFixture.Body(planned, new Order { Phone = "_Call", Customer = "a__b" }));
        Assert.Matches("^<div class=\"[^\"]+\"><label for=\"phone\" class=\"k[0-9a-f]+\">555</label>", EngineFixture.Body(targeted, new Order { Phone = "555" }));
        Assert.DoesNotContain("accesskey", EngineFixture.Body(targeted, new Order { Phone = "_Call" }), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-29")]
    public async Task ColorPickersPostThroughThePlanAsync()
    {
        var page = await fixture.CompileAsync("""<ColorPicker Color="{Binding Accent}" />""", new Palette());
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", page)]).Json, new Plans.AdapterSlotResolver()).Single().Page;

        foreach (var plan in new[] { page.Plan, saved.Plan })
        {
            var posted = new Palette { Accent = global::Avalonia.Media.Color.FromArgb(0x80, 0, 0, 0) };
            var refused = new Palette();
            Assert.Equal(PostStatus.Rendered, await PlanForms.ApplyAsync(plan, [posted], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", "#0a0B0c"), new Plans.AdapterSlotResolver()));
            Assert.Equal(PostStatus.Rendered, await PlanForms.ApplyAsync(plan, [refused], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("f0", "blue"), new Plans.AdapterSlotResolver()));
            Assert.Equal(global::Avalonia.Media.Color.FromArgb(0x80, 0x0a, 0x0b, 0x0c), posted.Accent);
            Assert.Equal(new Palette().Accent, refused.Accent);
        }
    }

    [Fact]
    [Trait("AC", "TEXT-30")]
    public async Task FontFamiliesAndTheirResourceFacesAreWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel TextElement.FontFamily="Courier New, Courier">
              <TextBlock Text="Inherited" />
              <TextBlock Text="Resource" FontFamily="avares://Avalonia.Fonts.Inter/Assets#Inter" />
            </StackPanel>
            """);
        var plain = await fixture.CompileAsync("""<StackPanel><TextBlock Text="Plain" /></StackPanel>""");

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').Single(rule => rule.StartsWith("." + Regex.Match(html, "class=\"([^\"]*\\s)?(k[0-9a-f]+)\"[^>]*>" + text).Groups[2].Value + "{", StringComparison.Ordinal));
        Assert.Contains("font-family:\"Courier New\",\"Courier\",Inter,system-ui,sans-serif;", Regex.Match(page.Css, "\\.(k[0-9a-f]+)\\{[^}]*font-family:\"Courier New\"[^}]*\\}").Value, StringComparison.Ordinal);
        Assert.DoesNotContain("font-family", Rule("Inherited"), StringComparison.Ordinal);
        Assert.Contains("font-family:\"Inter\",Inter,system-ui,sans-serif;", Rule("Resource"), StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Inter\";src:url(/_aioros/fonts/r/Avalonia.Fonts.Inter/Assets/Inter-Regular.ttf) format('truetype');font-weight:400;font-style:normal;font-display:swap}", page.Css, StringComparison.Ordinal);
        Assert.Contains("Inter-Thin.ttf) format('truetype');font-weight:250;", page.Css, StringComparison.Ordinal);
        Assert.Contains("Inter-Bold.ttf) format('truetype');font-weight:700;", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("font-family", plain.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-31")]
    public async Task ResourceFilesAreReadWithoutAvaloniaAsync()
    {
        var assembly = typeof(global::Avalonia.Fonts.Inter.InterFontCollection).Assembly;
        await using var opened = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://Avalonia.Fonts.Inter/Assets/Inter-Medium.ttf"));
        using var expected = new MemoryStream();
        await opened.CopyToAsync(expected, TestContext.Current.CancellationToken);

        Assert.Equal(expected.ToArray(), await Aioros.Avalonia.Hosting.AvaloniaResourceFiles.ReadAsync(assembly, "Assets/Inter-Medium.ttf", TestContext.Current.CancellationToken));
        Assert.Null(await Aioros.Avalonia.Hosting.AvaloniaResourceFiles.ReadAsync(assembly, "Assets/Missing.ttf", TestContext.Current.CancellationToken));
    }
    [Fact]
    [Trait("AC", "TEXT-33")]
    public async Task LabelLineHeightOfTheTemplateIsWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Snug" TargetType="Label">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" TextBlock.LineHeight="14" />
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <Label Theme="{StaticResource Snug}" Content="Email" />
              <Label Content="Fluent" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').Single(rule => rule.StartsWith("." + Regex.Match(html, "class=\"(k[0-9a-f]+)\">" + text).Groups[1].Value + "{", StringComparison.Ordinal));
        Assert.Contains("line-height:14px;", Rule("Email"), StringComparison.Ordinal);
        Assert.DoesNotContain("line-height", Rule("Fluent"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-39")]
    public async Task LetterSpacingAndFontStyleAreWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="TextBlock.tight"><Setter Property="LetterSpacing" Value="-0.9" /></Style>
                <Style Selector="TextBlock.quote"><Setter Property="FontStyle" Value="Italic" /></Style>
              </StackPanel.Styles>
              <StackPanel.Resources>
                <ControlTheme x:Key="Titled" TargetType="ContentControl">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" LetterSpacing="-0.45" />
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <TextBlock Classes="tight" Text="Heading" />
              <TextBlock Classes="quote" Text="Quote" />
              <ContentControl Theme="{StaticResource Titled}" Content="Title" />
              <ContentControl LetterSpacing="1.2" Content="Wide" />
              <TextBlock Text="Plain" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').Single(rule => rule.StartsWith("." + Regex.Match(html, "class=\"([^\"]*\\s)?(k[0-9a-f]+)\"[^>]*>" + text + "<").Groups[2].Value + "{", StringComparison.Ordinal));
        Assert.Contains("letter-spacing:-0.9px;", Rule("Heading"), StringComparison.Ordinal);
        Assert.Contains("font-style:italic;", Rule("Quote"), StringComparison.Ordinal);
        Assert.Contains("letter-spacing:-0.45px;", Rule("Title"), StringComparison.Ordinal);
        Assert.Contains("letter-spacing:1.2px;", Rule("Wide"), StringComparison.Ordinal);
        Assert.DoesNotContain("letter-spacing", Rule("Plain"), StringComparison.Ordinal);
        Assert.DoesNotContain("font-style", Rule("Plain"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-40")]
    public async Task MaxLinesClampTheTextAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Width="120">
              <TextBlock Text="Trimmed" TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis" />
              <TextBlock Text="Cut" TextWrapping="Wrap" MaxLines="2" />
              <TextBlock Text="Single" MaxLines="2" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css.Split('\n').Single(rule => rule.StartsWith("." + Regex.Match(html, "class=\"(k[0-9a-f]+)\">" + text + "<").Groups[1].Value + "{", StringComparison.Ordinal));
        Assert.Contains("display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:2;overflow:hidden;", Rule("Trimmed"), StringComparison.Ordinal);
        Assert.Contains("max-height:2lh;overflow:hidden;", Rule("Cut"), StringComparison.Ordinal);
        Assert.DoesNotContain("-webkit-line-clamp", Rule("Cut"), StringComparison.Ordinal);
        Assert.DoesNotMatch("line-clamp|max-height", Rule("Single"));
    }

    [Fact]
    [Trait("AC", "TEXT-41")]
    public async Task ATextBoxPostsAsItIsTypedAfterItsWebDelayAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBox a:Web.Delay="250" Text="{Binding Hinted}" />
              <TextBox Text="{Binding Bound, Delay=200}" />
              <TextBox a:Web.Delay="400" Text="{Binding Both, Delay=200}" />
              <TextBox a:Web.Delay="0" Text="{Binding Off, Delay=200}" />
              <TextBox Text="{Binding Plain}" />
            </StackPanel>
            """;
        var drafts = new Drafts();

        var html = await fixture.HtmlAsync(Body, drafts);
        var delays = Regex.Matches(html, "<input type=\"text\" name=\"f[0-9]\"(?<marks>[^>]*?) class=").Select(match => match.Groups["marks"].Value).ToList();
        await fixture.PostAsync(Body, drafts, ("f0", "a"), ("f1", "b"), ("f2", "c"), ("f3", "d"), ("f4", "e"));

        Assert.Equal([" data-change data-delay=\"250\"", " data-change data-delay=\"200\"", " data-change data-delay=\"400\"", "", ""], delays);
        Assert.Equal(("a", "b", "c", "d", "e"), (drafts.Hinted, drafts.Bound, drafts.Both, drafts.Off, drafts.Plain));
    }

    [Fact]
    [Trait("AC", "TEXT-42")]
    public async Task LinkedInlinesAreLinksInTheirTextAsync()
    {
        var page = await fixture.CompileAsync("""
            <TextBlock><Run Text="Don't have an account? " /><Run Text="Sign up" a:Web.Href="/signup" TextDecorations="Underline" /><Run Text=" or read the " /><Span a:Web.Href="/terms"><Bold>Terms</Bold></Span></TextBlock>
            """);
        var html = EngineFixture.Body(page);

        Assert.Matches(">Don't have an account\\? <a href=\"/signup\" data-nav class=\"il\"><span class=\"k[0-9a-f]+\">Sign up</span></a> or read the <a href=\"/terms\" data-nav class=\"il\"><span><b>Terms</b></span></a><", html);
        Assert.Contains(":where(a.il){color:inherit;text-decoration:inherit}", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-43")]
    public async Task CompositeFontFamiliesDeclareTheirFacesAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel><TextBlock Text="⌘P" FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Avalonia.Fonts.Inter/Assets#Inter" /></StackPanel>
            """);

        Assert.Contains("font-family:\"Cosmo Symbols\",\"Inter\",Inter,system-ui,sans-serif;", page.Css, StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Cosmo Symbols\";src:url(/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoSymbols-Regular.ttf) format('truetype');font-weight:400;", page.Css, StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Inter\";src:url(/_aioros/fonts/r/Avalonia.Fonts.Inter/Assets/Inter-Regular.ttf) format('truetype');font-weight:400;", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-44")]
    public async Task OneLineFieldsWithoutAWidthSizeToTheirTextAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Orientation="Horizontal">
              <TextBox PlaceholderText="&lt;M/d/yyyy&gt;" MinWidth="40" />
              <TextBox Width="200" PlaceholderText="Fixed" />
            </StackPanel>
            """);
        var html = EngineFixture.Body(page);
        string Rules(string placeholder) => string.Concat(Regex.Match(html, $"<input [^>]*class=\"(?<classes>[^\"]+)\"[^>]*placeholder=\"{Regex.Escape(placeholder)}\"").Groups["classes"].Value.Split(' ')
            .Select(name => string.Concat(Regex.Matches(page.Css, $"(?:input)?\\.{Regex.Escape(name)}\\{{(?<rule>[^}}]*)\\}}").Select(match => match.Groups["rule"].Value))));

        Assert.Contains("field-sizing:content", Rules("&lt;M/d/yyyy&gt;"), StringComparison.Ordinal);
        Assert.Contains("min-width:40px", Rules("&lt;M/d/yyyy&gt;"), StringComparison.Ordinal);
        Assert.DoesNotContain("field-sizing", Rules("Fixed"), StringComparison.Ordinal);
        Assert.Contains("width:200px", Rules("Fixed"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-45")]
    public async Task ColorFontsNameTheirTechnologyAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="&#x1F44D;" FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Emoji" />
              <TextBlock Text="&#x2318;" FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols" />
            </StackPanel>
            """);

        Assert.Contains("@font-face{font-family:\"Cosmo Emoji\";src:url(/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoEmoji-Regular.ttf) format('truetype') tech(color-CBDT);", page.Css, StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Cosmo Symbols\";src:url(/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoSymbols-Regular.ttf) format('truetype');", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-52")]
    public async Task TextWrapHintsAreWrittenAsCssTextWrapAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock a:Web.TextWrap="balance" TextWrapping="Wrap" Text="Balanced lines" />
              <TextBlock a:Web.TextWrap="pretty" TextWrapping="Wrap" Text="Pretty lines" />
              <TextBlock a:Web.TextWrap="bogus" TextWrapping="Wrap" Text="Plain lines" />
              <ContentControl a:Web.TextWrap="balance" Content="Presented lines" />
            </StackPanel>
            """);

        var html = EngineFixture.Body(page);
        string Rule(string text) => page.Css[..].Split('}').First(rule => rule.Contains("." + System.Text.RegularExpressions.Regex.Match(html, "class=\"(?:[^\"]* )?(k[0-9a-f]+)\"[^>]*>" + text).Groups[1].Value + "{", StringComparison.Ordinal));
        Assert.Contains("text-wrap:balance;", Rule("Balanced lines"), StringComparison.Ordinal);
        Assert.Contains("text-wrap:pretty;", Rule("Pretty lines"), StringComparison.Ordinal);
        Assert.DoesNotContain("text-wrap", Rule("Plain lines"), StringComparison.Ordinal);
        Assert.Contains("text-wrap:balance;", Rule("Presented lines"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-54")]
    public async Task AShimmeringTextIsWrittenAsShadcnsShimmerAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBlock a:Web.Shimmer="True" Text="Uploading" />
              <ContentControl a:Web.Shimmer="True" Content="Processing" />
              <TextBlock Text="Done" />
            </StackPanel>
            """;
        var light = await fixture.CompileAsync(Body);
        var dark = await fixture.CompileAsync(Body, theme: "dark");

        string Classes(CompiledPage page, string text) => System.Text.RegularExpressions.Regex.Match(EngineFixture.Body(page), "class=\"([^\"]*)\"[^>]*>" + text).Groups[1].Value;
        Assert.Contains("shm", Classes(light, "Uploading").Split(' '));
        Assert.Contains("shm", Classes(light, "Processing").Split(' '));
        Assert.DoesNotContain("shm", Classes(light, "Done").Split(' '));
        Assert.Contains("shmd", Classes(dark, "Uploading").Split(' '));
        Assert.Contains(".shm{--_highlight:var(--shimmer-color,oklch(from currentColor l c h / calc(alpha * 0.2)));", light.Css, StringComparison.Ordinal);
        Assert.Contains(".shmd{--_highlight:var(--shimmer-color,oklch(from currentColor max(0.8, calc(l + 0.4)) c h / calc(alpha + 0.4)));", dark.Css, StringComparison.Ordinal);
        Assert.Contains("background-size:calc(200% + var(--_spread) * 2) 100%;background-position:0 0;background-clip:text;", light.Css, StringComparison.Ordinal);
        Assert.Contains("animation:ashm 2s linear infinite}", light.Css, StringComparison.Ordinal);
        Assert.Contains("@keyframes ashm{from{background-position:100% 0}to{background-position:0 0}}", light.Css, StringComparison.Ordinal);
        Assert.Contains(":where(.shm,.shmd):where([dir=rtl],[dir=rtl] *){animation-direction:reverse}", light.Css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion:reduce){:where(.shm,.shmd){animation:none;background-image:none;-webkit-text-fill-color:currentColor}}", light.Css, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(light.Css, "@keyframes ashm"));
    }
    [Fact]
    [Trait("AC", "TEXT-55")]
    public async Task AnUnderlineMovedByItsStrokeOffsetIsWrittenAtTheSameDistanceAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="HyperlinkButton">
                  <Setter Property="TextBlock.TextDecorations">
                    <TextDecorationCollection><TextDecoration Location="Underline" StrokeOffset="0.25" StrokeOffsetUnit="FontRenderingEmSize" /></TextDecorationCollection>
                  </Setter>
                </Style>
              </StackPanel.Styles>
              <TextBlock FontSize="16"><Span><Span.TextDecorations><TextDecorationCollection><TextDecoration Location="Underline" StrokeOffset="3" StrokeOffsetUnit="Pixel" StrokeThickness="2" StrokeThicknessUnit="Pixel" /></TextDecorationCollection></Span.TextDecorations>moved</Span><Span><Span.TextDecorations><TextDecorationCollection><TextDecoration Location="Underline" StrokeOffset="3" StrokeOffsetUnit="Pixel" StrokeThickness="4" StrokeThicknessUnit="Pixel" /></TextDecorationCollection></Span.TextDecorations>thick</Span><Underline>plain</Underline></TextBlock>
              <TextBlock FontSize="16" Text="block"><TextBlock.TextDecorations><TextDecorationCollection><TextDecoration Location="Underline" StrokeOffset="3" StrokeOffsetUnit="Pixel" StrokeThickness="2" StrokeThicknessUnit="Pixel" /></TextDecorationCollection></TextBlock.TextDecorations></TextBlock>
              <TextBlock TextDecorations="Strikethrough" Text="struck" />
              <HyperlinkButton NavigateUri="/a" FontSize="16" Content="Link" />
            </StackPanel>
            """);

        var body = EngineFixture.Body(page);
        double Offset(string text)
        {
            var classes = Regex.Match(body, "class=\"([^\"]*)\"[^>]*>" + text).Groups[1].Value.Split(' ');
            var offsets = classes.Select(name => Regex.Match(page.Css, "\\." + name + "\\{[^}]*text-underline-offset:([0-9.]+)px;")).Where(match => match.Success).ToList();
            return double.Parse(Assert.Single(offsets).Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var position = Offset("moved") - 3 + 1;
        Assert.InRange(position, 0.5, 4);
        Assert.Equal(position - 2 + 3, Offset("thick"), 3);
        Assert.Equal(position - 1 + 3, Offset("block"), 3);
        var struck = Regex.Match(body, "class=\"([^\"]*)\"[^>]*>struck").Groups[1].Value.Split(' ');
        Assert.Contains(struck, name => Regex.IsMatch(page.Css, "\\." + name + "\\{[^}]*text-decoration:line-through;"));
        Assert.DoesNotContain("text-underline-offset", Regex.Match(body, "<u[^>]*>plain").Value, StringComparison.Ordinal);
        Assert.InRange(position + 4 - Offset("Link"), 0.25, 1);
    }
    [Fact]
    [Trait("AC", "TEXT-56")]
    public async Task AnEmbeddedControlStandsAsItsBaselineAlignmentSaysAsync()
    {
        var page = await fixture.CompileAsync("""
            <TextBlock><Run Text="a" /><InlineUIContainer><Border Width="8" Height="8" Background="Red" /></InlineUIContainer><InlineUIContainer BaselineAlignment="Center"><Border Width="8" Height="8" Background="Green" /></InlineUIContainer><InlineUIContainer BaselineAlignment="Top"><Border Width="8" Height="8" Background="Blue" /></InlineUIContainer></TextBlock>
            """);

        var spans = Regex.Matches(EngineFixture.Body(page), "<span class=\"([^\"]+)\"><div").Select(match => Regex.Match(page.Css, $@"\.{match.Groups[1].Value}\{{([^}}]*)\}}").Groups[1].Value).ToList();
        Assert.Equal(["display:inline-flex;vertical-align:baseline;", "display:inline-flex;vertical-align:middle;", "display:inline-flex;vertical-align:top;"], spans);
    }
    [Fact]
    [Trait("AC", "TEXT-57")]
    public async Task FontFeaturesAreWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock FontFeatures="+tnum" Text="1 / 2" />
              <TextBlock FontFeatures="+tnum, -liga" Text="fi 10" />
              <TextBlock Text="plain" />
            </StackPanel>
            """);
        var body = EngineFixture.Body(page);
        string Rules(string text) => string.Concat(Regex.Match(body, "class=\"([^\"]*)\"[^>]*>" + text).Groups[1].Value.Split(' ').Select(name => Regex.Match(page.Css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

        Assert.Contains("font-feature-settings:\"tnum\" 1;", Rules("1 / 2"), StringComparison.Ordinal);
        Assert.Contains("font-feature-settings:\"tnum\" 1,\"liga\" 0;", Rules("fi 10"), StringComparison.Ordinal);
        Assert.DoesNotContain("font-feature-settings", Rules("plain"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-58")]
    public async Task FallbacksAfterTheInheritedFamilyAreWrittenAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel TextElement.FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Avalonia.Fonts.Inter/Assets#Inter">
              <TextBlock Text="Inherited" />
              <TextBlock Text="Repeated" FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Avalonia.Fonts.Inter/Assets#Inter" />
              <TextBlock Text="Extended" FontFamily="avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Emoji" />
            </StackPanel>
            """);
        var body = EngineFixture.Body(page);
        string Rules(string text) => string.Concat(Regex.Match(body, "class=\"([^\"]*)\"[^>]*>" + text).Groups[1].Value.Split(' ').Select(name => Regex.Match(page.Css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

        Assert.DoesNotContain("font-family", Rules("Inherited"), StringComparison.Ordinal);
        Assert.DoesNotContain("font-family", Rules("Repeated"), StringComparison.Ordinal);
        Assert.Contains("font-family:\"Cosmo Symbols\",\"Cosmo Emoji\",Inter,system-ui,sans-serif;", Rules("Extended"), StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Cosmo Emoji\";src:url(/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoEmoji-Regular.ttf) format('truetype')", page.Css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "TEXT-59")]
    public async Task RelativeFontSourcesAreResolvedAgainstTheirFileAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/RelativeFonts.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <TextBlock Text="Relative" FontFamily="{StaticResource RelativeEmoji}" />
              <TextBlock Text="Fallback" FontFamily="{StaticResource RelativeFallback}" />
              <TextBlock Text="Unresolved" FontFamily="/Assets/Fonts#Cosmo Symbols" />
            </StackPanel>
            """);
        var body = EngineFixture.Body(page);
        string Rules(string text) => string.Concat(Regex.Match(body, "class=\"([^\"]*)\"[^>]*>" + text).Groups[1].Value.Split(' ').Select(name => Regex.Match(page.Css, "\\." + name + "\\{([^}]*)\\}").Groups[1].Value));

        Assert.Contains("font-family:\"Cosmo Emoji\",Inter,system-ui,sans-serif;", Rules("Relative"), StringComparison.Ordinal);
        Assert.Contains("font-family:\"Inter\",\"Cosmo Emoji\",Inter,system-ui,sans-serif;", Rules("Fallback"), StringComparison.Ordinal);
        Assert.Contains("font-family:\"Cosmo Symbols\",Inter,system-ui,sans-serif;", Rules("Unresolved"), StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"Cosmo Emoji\";src:url(/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoEmoji-Regular.ttf) format('truetype')", page.Css, StringComparison.Ordinal);
        Assert.DoesNotContain("@font-face{font-family:\"Cosmo Symbols\"", page.Css, StringComparison.Ordinal);
    }
}
