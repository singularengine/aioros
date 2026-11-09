using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class DialogPostTests(EngineFixture fixture)
{
    private static string Page(bool lightDismiss = true, bool alert = false, bool fading = false) => $"""
        <StackPanel>
          <m:PromptHost Height="400" />
          <m:Modal IsOpen="{"{"}Binding Open{"}"}" LightDismiss="{lightDismiss}" Alert="{alert}" Fading="{fading}" />
          <Button Command="{"{"}Binding ShowCommand{"}"}" Content="Show" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-309")]
    public async Task APostThePlanAnswersIsNotWrittenUnlessItLeftAPromptAsync()
    {
        static ImmutableDictionary<string, string> Form(string pressed) => ImmutableDictionary<string, string>.Empty.Add("_btn", pressed);
        const string Plain = """<StackPanel><TextBox Text="{Binding Name}" /><Button Content="Save" Command="{Binding SaveCommand}" CommandParameter="{Binding Token}" /></StackPanel>""";
        var signup = new Signup();
        var token = signup.Token;

        var saved = await fixture.Engine.PostXamlAsync(ControlHarness.Xaml(Plain, nameof(Signup)), [signup], Form("0|" + token), [], "light", TestContext.Current.CancellationToken, planAnswers: true);
        var opened = await fixture.Engine.PostXamlAsync(ControlHarness.Xaml(Page(), nameof(Opening)), [new Opening()], Form("0|"), [], "light", TestContext.Current.CancellationToken, planAnswers: true);

        Assert.Equal((PostStatus.Rendered, false, 1), (saved.Status, saved.Page is not null, signup.Saved));
        Assert.True(opened.Shows);
        Assert.Contains("<dialog open class=\"prompt\"", EngineFixture.Body(opened.Page!, new Opening()), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-213")]
    public async Task ADialogOpenedByACommandThatEndedIsAPromptAsync()
    {
        var opened = await PostAsync(Page(), new Opening(), ("_btn", "0|"));
        var html = EngineFixture.Body(opened.Page!, new Opening());
        var model = new Opening();
        var closed = await PostAsync(Page(), model, ("_prompt", Prompt(html)), ("_btn", Button(html, "Close")));

        Assert.True(opened.Shows);
        Assert.Contains("<dialog open class=\"prompt\"", html, StringComparison.Ordinal);
        Assert.Equal("_btn=0%7C", Prompt(html));
        Assert.Equal(PostStatus.Rendered, closed.Status);
        Assert.Equal(1, model.Hidden);
        Assert.False(model.Open);
        Assert.DoesNotContain("<dialog", EngineFixture.Body(closed.Page!, model), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-214")]
    public async Task EscapeAndAClickOutsideAreHandedToTheDialogAsync()
    {
        var html = EngineFixture.Body((await PostAsync(Page(), new Opening(), ("_btn", "0|"))).Page!, new Opening());
        var escaped = new Opening();
        var escape = await PostAsync(Page(), escaped, ("_prompt", Prompt(html)), ("_dismiss", "escape"));
        var outside = new Opening();
        var clicked = await PostAsync(Page(), outside, ("_prompt", Prompt(html)), ("_dismiss", "outside"));
        var kept = EngineFixture.Body((await PostAsync(Page(lightDismiss: false), new Opening(), ("_btn", "0|"))).Page!, new Opening());
        var stays = await PostAsync(Page(lightDismiss: false), new Opening(), ("_prompt", Prompt(kept)), ("_dismiss", "outside"));

        Assert.Equal(PostStatus.Rendered, escape.Status);
        Assert.False(escaped.Open);
        Assert.DoesNotContain("<dialog", EngineFixture.Body(escape.Page!, escaped), StringComparison.Ordinal);
        Assert.False(outside.Open);
        Assert.DoesNotContain("<dialog", EngineFixture.Body(clicked.Page!, outside), StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, stays.Status);
        Assert.True(stays.Shows);
        Assert.StartsWith("_btn=0%7C", Prompt(EngineFixture.Body(stays.Page!, new Opening())), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-215")]
    public async Task APromptIsLabelledAndLaidOutByItsSurfaceAsync()
    {
        var opened = await PostAsync(Page(), new Opening(), ("_btn", "0|"));
        var html = EngineFixture.Body(opened.Page!, new Opening());
        var alert = EngineFixture.Body((await PostAsync(Page(alert: true), new Opening(), ("_btn", "0|"))).Page!, new Opening());
        var dialog = html[html.IndexOf("<dialog", StringComparison.Ordinal)..];
        var surface = Regex.Match(dialog, "</div><div class=\"c (k[0-9a-f]+)\">").Groups[1].Value;

        Assert.StartsWith("<dialog open class=\"prompt\" aria-labelledby=\"prompt1-title\" aria-describedby=\"prompt1-description\"", dialog, StringComparison.Ordinal);
        Assert.Matches("<div id=\"prompt1-title\"[^>]*>Edit profile</div><div id=\"prompt1-description\"[^>]*>Make changes to your profile here\\.</div>", dialog);
        Assert.Matches("<div class=\"pm k[0-9a-f]+\"></div>", dialog);
        Assert.Contains($".{surface}{{grid-area:1/1;min-height:0;min-width:0;max-width:100%;max-height:100%;overflow:auto;justify-self:center;align-self:center;}}", opened.Page!.Css, StringComparison.Ordinal);
        Assert.StartsWith("<dialog open class=\"prompt\" role=\"alertdialog\"", alert[alert.IndexOf("<dialog", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"[^\"]+\" autofocus[^>]*>Cancel</button>", alert);
        Assert.DoesNotContain("role=\"alert\"", alert, StringComparison.Ordinal);
        Assert.DoesNotContain("autofocus", dialog, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "MOTION-13")]
    public async Task APromptsMaskFadesInAsItsThemeDoesAsync()
    {
        var opened = await PostAsync(Page(fading: true), new Opening(), ("_btn", "0|"));
        var plain = await PostAsync(Page(), new Opening(), ("_btn", "0|"));
        var mask = Regex.Match(EngineFixture.Body(opened.Page!, new Opening()), "<div class=\"pm (k[0-9a-f]+) (en[0-9a-f]+)\"></div>");
        var still = Regex.Match(EngineFixture.Body(plain.Page!, new Opening()), "<div class=\"(pm k[0-9a-f]+)\"></div>");

        Assert.True(mask.Success, EngineFixture.Body(opened.Page!, new Opening()));
        Assert.Contains("transition:opacity 150ms linear 0ms;", Regex.Match(opened.Page!.Css, $"\\.{mask.Groups[1].Value}\\{{([^}}]*)\\}}").Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains($"@starting-style{{.{mask.Groups[2].Value}.{mask.Groups[2].Value}.{mask.Groups[2].Value}.{mask.Groups[2].Value}{{opacity:0;}}}}", opened.Page.Css, StringComparison.Ordinal);
        Assert.True(still.Success);
    }

    [Fact]
    [Trait("AC", "ENGINE-216")]
    public async Task ANoticesButtonPostsItsCommandAsync()
    {
        const string Body = """
            <StackPanel>
              <m:ToastHost />
              <Button Command="{Binding ActCommand}" Content="Act" />
            </StackPanel>
            """;

        var acted = await PostAsync(Body, new Toasting(), ("_btn", "0|"));
        var html = EngineFixture.Body(acted.Page!, new Toasting());
        var replay = WebUtility.HtmlDecode(Regex.Match(html, "class=\"notice\" data-replay=\"([^\"]*)\"").Groups[1].Value);
        var model = new Toasting();
        var undone = await PostAsync(Body, model, ("_prompt", replay), ("_btn", Button(html, "Undo")));

        Assert.Equal("_btn=0%7C", replay);
        Assert.Equal(PostStatus.Rendered, undone.Status);
        Assert.Equal(1, model.Undone);
        Assert.DoesNotContain("class=\"notice", EngineFixture.Body(undone.Page!, model), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-271")]
    public async Task ANoticesButtonPostsItsCommandWithoutTheScriptAsync()
    {
        const string Body = """
            <StackPanel>
              <m:ToastHost />
              <Button Command="{Binding ActCommand}" Content="Act" />
            </StackPanel>
            """;

        var acted = await PostAsync(Body, new Toasting(), ("_btn", "0|"));
        var html = EngineFixture.Body(acted.Page!, new Toasting());
        var notice = WebUtility.HtmlDecode(Regex.Match(html, "<input type=\"hidden\" name=\"_notice\" value=\"([^\"]*)\">").Groups[1].Value);
        var undoing = new Toasting();
        var undone = await NativeAsync(Body, undoing, notice, Button(html, "Undo"));
        var again = new Toasting();
        var reacted = await NativeAsync(Body, again, notice, Button(html, "Act"));

        Assert.Equal(Button(html, "Undo").Split('|')[0] + "\n_btn=0%7C", notice);
        Assert.Equal((PostStatus.Rendered, 1, 1), (undone.Status, undoing.Shown, undoing.Undone));
        Assert.Equal((PostStatus.Rendered, 1, 0), (reacted.Status, again.Shown, again.Undone));
    }

    private Task<PostOutcome> NativeAsync(string body, object model, string notice, string button)
    {
        var form = PlanForms.Overlays(System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("_notice", notice).Add("_btn", button));
        return PostAsync(body, model, [.. form.Select(pair => (pair.Key, pair.Value))]);
    }

    [Fact]
    [Trait("AC", "NAVIGATION-89")]
    public async Task NoticesStandWhereTheirPanelStandsThemAsync()
    {
        const string Deck = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Deck" TargetType="WindowNotificationManager">
                  <Setter Property="Margin" Value="24" />
                  <Setter Property="Template">
                    <ControlTemplate><Grid Name="PART_Items" HorizontalAlignment="Right" VerticalAlignment="Bottom" /></ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <m:DeckHost Deck="{StaticResource Deck}" />
              <Button Command="{Binding ToastCommand}" Content="Toast" />
            </StackPanel>
            """;
        const string Column = """
            <StackPanel>
              <m:ToastHost />
              <Button Command="{Binding ToastCommand}" Content="Toast" />
            </StackPanel>
            """;

        var column = EngineFixture.Body((await PostAsync(Column, new Toasting(), ("_btn", "0|"))).Page!, new Toasting());
        var deck = EngineFixture.Body((await PostAsync(Deck, new Toasting(), ("_btn", "0|"))).Page!, new Toasting());

        Assert.Matches("<div popover=\"manual\" class=\"notice(?: nr)?\" data-replay=\"[^\"]*\" style=\"top:0px;right:0px;\">", column);
        Assert.Matches("<div popover=\"manual\" class=\"notice na\" data-replay=\"[^\"]*\" style=\"bottom:24px;right:24px;width:[0-9.]+px;height:[0-9.]+px;\">", deck);
        Assert.Equal(2, Regex.Count(deck, "<div class=\"nt[^\"]*\" role=\"status\" style=\"--expire:[0-9]+ms;position:absolute;left:[0-9.]+px;width:[0-9.]+px;bottom:[0-9.]+px;\">"));
        Assert.Contains("<div class=\"nt\" role=\"status\" style=\"--expire:5000ms;", column, StringComparison.Ordinal);
        Assert.Contains("<div class=\"nt nk\" role=\"status\" style=\"--expire:0ms;", column, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SSR-237")]
    public async Task ANoticeIsShownWithoutJavaScriptAsync()
    {
        const string Column = """
            <StackPanel>
              <m:ToastHost />
              <Button Command="{Binding ToastCommand}" Content="Toast" />
            </StackPanel>
            """;
        var posted = await PostAsync(Column, new Toasting(), ("_btn", "0|"));
        var body = EngineFixture.Body(posted.Page!, new Toasting());

        var off = await BrowserLayout.ShownAsync(posted.Page!, body, javaScript: false, ".notice");
        var on = await BrowserLayout.ShownAsync(posted.Page!, body, javaScript: true, ".notice");

        Assert.True(off.Visible);
        Assert.True(off.Box is { Width: > 0, Height: > 0 } box && box.Y == 0 && Math.Abs(box.X + box.Width - 800) < 1, $"{off.Box?.X} {off.Box?.Y} {off.Box?.Width} {off.Box?.Height}");
        Assert.False(on.Visible);
    }

    [Fact]
    [Trait("AC", "MOTION-17")]
    public async Task NoticesRiseAndLeaveAsTheirCardsThemeAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Rising" TargetType="NotificationCard">
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" Background="#ffffff" m:Fade.Enter="True" m:Fade.Lift="8">
                        <Border.Transitions>
                          <Transitions>
                            <DoubleTransition Property="Opacity" Duration="0:0:0.4" />
                            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.4" />
                          </Transitions>
                        </Border.Transitions>
                        <ContentPresenter Content="{TemplateBinding Content}" />
                      </Border>
                    </ControlTemplate>
                  </Setter>
                  <Style Selector="^:closing /template/ Border#PART_Root">
                    <Setter Property="Opacity" Value="0" />
                    <Setter Property="RenderTransform" Value="translate(0px,20px)" />
                  </Style>
                </ControlTheme>
              </StackPanel.Resources>
              <m:RisingHost CardTheme="{StaticResource Rising}" />
              <Button Command="{Binding ToastCommand}" Content="Toast" />
            </StackPanel>
            """;

        var page = (await PostAsync(Body, new Toasting(), ("_btn", "0|"))).Page!;
        var html = EngineFixture.Body(page, new Toasting());
        var leaving = Regex.Match(html, "<div class=\"nt (n[0-9a-f]+)\" role=\"status\"").Groups[1].Value;
        var entering = Regex.Match(html, "class=\"c (en[0-9a-f]+) ").Groups[1].Value;

        Assert.Contains($".notice>.nt.{leaving}{{animation:{leaving} 400ms linear forwards;animation-delay:var(--expire)}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"@keyframes {leaving}{{to{{opacity:0;transform:matrix(1,0,0,1,0,20);visibility:hidden}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"@media (prefers-reduced-motion:reduce){{.notice>.nt.{leaving}{{animation-duration:0s}}}}", page.Css, StringComparison.Ordinal);
        Assert.Contains($"@starting-style{{.{entering}.{entering}.{entering}.{entering}{{opacity:0;transform:matrix(1,0,0,1,0,-8);}}}}", page.Css, StringComparison.Ordinal);
    }

    private async Task<PostOutcome> PostAsync(string body, object model, params (string Key, string Value)[] fields) =>
        await fixture.PostAsync(body, model, fields).WaitAsync(TestContext.Current.CancellationToken);

    private static string Prompt(string html) => WebUtility.HtmlDecode(PromptPattern().Match(html).Groups[1].Value);

    private static string Button(string html, string text) => ButtonPattern().Matches(html).First(match => match.Groups[2].Value == text).Groups[1].Value;

    [GeneratedRegex("name=\"_prompt\" value=\"([^\"]*)\"")]
    private static partial Regex PromptPattern();

    [GeneratedRegex("name=\"_btn\" value=\"([^\"]*)\"[^>]*>(?:<[^>]+>)*([^<]*)<")]
    private static partial Regex ButtonPattern();
}
