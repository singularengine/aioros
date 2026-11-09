using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Hosting;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed partial class PromptPostTests(EngineFixture fixture)
{
    private const string Confirm = """
        <StackPanel>
          <TextBox Text="{Binding Name}" />
          <m:PromptHost />
          <Button Command="{Binding AskCommand}" Content="Ask" />
          <TextBlock Text="{Binding Answer}" />
        </StackPanel>
        """;

    private const string Notices = """
        <StackPanel>
          <m:NoticeHost />
          <m:PromptHost />
          <Button Command="{Binding RestCommand}" Content="Rest" />
          <Button Command="{Binding NotifyCommand}" Content="Notify" />
          <Button Command="{Binding AskCommand}" Content="Ask" />
        </StackPanel>
        """;

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-75")]
    public async Task PostsAnswerOnceACommandWaitsOnAPromptAsync()
    {
        const string Body = """
            <StackPanel>
              <m:PromptHost />
              <Button Command="{Binding AskCommand}" Content="Ask" />
            </StackPanel>
            """;

        var asked = await fixture.PostAsync(Body, new Asking(), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken);
        var (listed, prompted) = await fixture.Engine.DispatchAsync(Opened, TestContext.Current.CancellationToken);

        Assert.Equal(PostStatus.Rendered, asked.Status);
        Assert.False(listed);
        Assert.True(prompted);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-143")]
    public async Task APromptIsWrittenAsADialogOverItsHostAsync()
    {
        var before = await fixture.HtmlAsync(Confirm, new Confirming());

        var asked = await fixture.PostAsync(Confirm, new Confirming(), ("f0", "Ada"), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken);
        var html = EngineFixture.Body(asked.Page!, new Confirming());
        var dialog = html.IndexOf("<dialog open class=\"prompt\"", StringComparison.Ordinal);
        var anchor = AnchorPattern().Match(html).Groups[1].Value;

        Assert.True(asked.Shows);
        Assert.Contains("name=\"f0\"", before, StringComparison.Ordinal);
        Assert.Contains("value=\"0|\"", before, StringComparison.Ordinal);
        Assert.Contains("name=\"f0\"", html[..dialog], StringComparison.Ordinal);
        Assert.Contains("value=\"0|\"", html[..dialog], StringComparison.Ordinal);
        Assert.True(dialog > html.IndexOf(">Ask<", StringComparison.Ordinal));
        Assert.Equal("_btn=0%7C", Prompt(html));
        Assert.NotEmpty(anchor);
        Assert.Contains("anchor-name:--" + anchor, asked.Page!.Css, StringComparison.Ordinal);
        Assert.Contains(">OK<", html[dialog..], StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-144")]
    public async Task APromptsButtonResumesTheCommandThatWaitsAsync()
    {
        var shown = await AskAsync();
        var model = new Confirming();

        var answered = await fixture.PostAsync(Confirm, model, ("f0", "Ada"), ("_prompt", Prompt(shown)), (PromptText(shown), "Yes"), ("_btn", Button(shown, "OK")))
            .WaitAsync(TestContext.Current.CancellationToken);
        var html = EngineFixture.Body(answered.Page!, model);

        Assert.Equal(PostStatus.Rendered, answered.Status);
        Assert.False(answered.Shows);
        Assert.Equal("Ada: Yes (0)", model.Answer);
        Assert.Equal((1, 1), (model.Asked, model.Answered));
        Assert.DoesNotContain("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("Ada: Yes (0)", html, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-145")]
    public async Task APromptLeftOpenRecordsItsStepsAndReplaysThemAsync()
    {
        var shown = await AskAsync();
        var nudged = await fixture.PostAsync(Confirm, new Confirming(), ("f0", "Ada"), ("_prompt", Prompt(shown)), (PromptText(shown), "Maybe"), ("_btn", Button(shown, "Nudge")))
            .WaitAsync(TestContext.Current.CancellationToken);
        var again = EngineFixture.Body(nudged.Page!, new Confirming());
        var steps = Prompt(again).Split('\n');
        var model = new Confirming();

        var answered = await fixture.PostAsync(Confirm, model, ("f0", "Ada"), ("_prompt", Prompt(again)), (PromptText(again), "Sure"), ("_btn", Button(again, "OK")))
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(nudged.Shows);
        Assert.Contains("Nudged 1", again, StringComparison.Ordinal);
        Assert.Equal(2, steps.Length);
        Assert.Equal("_btn=0%7C", steps[0]);
        Assert.Contains(Uri.EscapeDataString(PromptText(shown)) + "=Maybe", steps[1], StringComparison.Ordinal);
        Assert.False(answered.Shows);
        Assert.Equal("Ada: Sure (1)", model.Answer);
        Assert.Equal((1, 1), (model.Asked, model.Answered));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-301")]
    public async Task APromptReplaysAtMostSixtyFourStepsAsync()
    {
        var shown = await AskAsync();
        var nudged = await fixture.PostAsync(Confirm, new Confirming(), ("f0", "Ada"), ("_prompt", Prompt(shown)), (PromptText(shown), "Maybe"), ("_btn", Button(shown, "Nudge")))
            .WaitAsync(TestContext.Current.CancellationToken);
        var again = EngineFixture.Body(nudged.Page!, new Confirming());
        var steps = Prompt(again).Split('\n');
        var (forged, kept) = (new Confirming(), new Confirming());

        var refused = await fixture.PostAsync(Confirm, forged, ("f0", "Ada"), ("_prompt", string.Join('\n', [steps[0], .. Enumerable.Repeat(steps[1], 64)])), (PromptText(again), "Sure"), ("_btn", Button(again, "Nudge")))
            .WaitAsync(TestContext.Current.CancellationToken);
        var replayed = await fixture.PostAsync(Confirm, kept, ("f0", "Ada"), ("_prompt", string.Join('\n', [steps[0], .. Enumerable.Repeat(steps[1], 63)])), (PromptText(again), "Sure"), ("_btn", Button(again, "Nudge")))
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((PostStatus.BadRequest, 0), (refused.Status, forged.Asked));
        Assert.Equal((PostStatus.Rendered, 1), (replayed.Status, kept.Asked));
        Assert.Contains("Nudged 64", EngineFixture.Body(replayed.Page!, new Confirming()), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-272")]
    public async Task APromptsStepsReplayWithTheBrowsersLineBreaksAsync()
    {
        var shown = await AskAsync();
        var nudged = await fixture.PostAsync(Confirm, new Confirming(), ("f0", "Ada"), ("_prompt", Prompt(shown)), (PromptText(shown), "Maybe"), ("_btn", Button(shown, "Nudge")))
            .WaitAsync(TestContext.Current.CancellationToken);
        var again = EngineFixture.Body(nudged.Page!, new Confirming());
        var model = new Confirming();

        var answered = await fixture.PostAsync(Confirm, model, ("f0", "Ada"), ("_prompt", Prompt(again).Replace("\n", "\r\n", StringComparison.Ordinal)), (PromptText(again), "Sure"), ("_btn", Button(again, "OK")))
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\n", Prompt(again), StringComparison.Ordinal);
        Assert.False(answered.Shows);
        Assert.Equal("Ada: Sure (1)", model.Answer);
        Assert.Equal((1, 1), (model.Asked, model.Answered));
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-273")]
    public async Task OnlyThePromptsOwnControlsResumeItWithoutTheScriptAsync()
    {
        var asked = await fixture.PostAsync(Notices, new Notifying(), ("_btn", "2|")).WaitAsync(TestContext.Current.CancellationToken);
        var html = EngineFixture.Body(asked.Page!, new Notifying());
        var answers = WebUtility.HtmlDecode(AnswersPattern().Match(html).Groups[1].Value);
        var resting = new Notifying();
        var rested = await NativeAsync(Notices, resting, html, Button(html, "Rest"));
        var notifying = new Notifying();
        var notified = await NativeAsync(Notices, notifying, html, Button(html, "Notify", "<dialog"));

        Assert.Equal(Button(html, "Notify", "<dialog").Split('|')[0] + " " + Button(html, "Close", "<dialog").Split('|')[0], answers);
        Assert.Equal((1, false), (resting.Rested, rested.Shows));
        Assert.DoesNotContain("<dialog", EngineFixture.Body(rested.Page!, resting), StringComparison.Ordinal);
        Assert.Equal((1, true), (notifying.Notified, notified.Shows));
        Assert.Contains("<dialog", EngineFixture.Body(notified.Page!, notifying), StringComparison.Ordinal);
    }

    private Task<PostOutcome> NativeAsync(string body, object model, string html, string button)
    {
        var form = PlanForms.Overlays(System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
            .Add("_prompt", Prompt(html)).Add("_answers", WebUtility.HtmlDecode(AnswersPattern().Match(html).Groups[1].Value)).Add("_btn", button));
        return fixture.PostAsync(body, model, [.. form.Select(pair => (pair.Key, pair.Value))]).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-146")]
    public async Task AReplayThatDivergesIsAConflictAsync()
    {
        var model = new Notifying();
        var diverged = await fixture.PostAsync(Notices, model, ("_prompt", "_btn=0%7C"), ("_btn", "1|")).WaitAsync(TestContext.Current.CancellationToken);
        var malformed = await fixture.PostAsync(Notices, new Notifying(), ("_prompt", "f0=1"), ("_btn", "1|")).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PostStatus.Conflict, diverged.Status);
        Assert.Equal(0, model.Notified);
        Assert.Equal(PostStatus.BadRequest, malformed.Status);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-147")]
    public async Task ANotificationIsWrittenAsANoticeAtItsCornerAsync()
    {
        var notified = await fixture.PostAsync(Notices, new Notifying(), ("_btn", "1|")).WaitAsync(TestContext.Current.CancellationToken);
        var rested = await fixture.PostAsync(Notices, new Notifying(), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken);
        var asked = EngineFixture.Body((await fixture.PostAsync(Notices, new Notifying(), ("_btn", "2|")).WaitAsync(TestContext.Current.CancellationToken)).Page!);
        var step = await fixture.PostAsync(Notices, new Notifying(), ("_prompt", Prompt(asked)), ("_btn", Button(asked, "Notify", after: "<dialog"))).WaitAsync(TestContext.Current.CancellationToken);
        var stepped = EngineFixture.Body(step.Page!);
        var closed = await fixture.PostAsync(Notices, new Notifying(), ("_prompt", Prompt(stepped)), ("_btn", Button(stepped, "Close"))).WaitAsync(TestContext.Current.CancellationToken);
        var html = EngineFixture.Body(notified.Page!);
        var notice = NoticePattern().Match(html);

        Assert.True(notified.Shows);
        Assert.True(notice.Success);
        Assert.Contains("top:0", notice.Value, StringComparison.Ordinal);
        Assert.Contains("right:0", notice.Value, StringComparison.Ordinal);
        Assert.Contains("The draft is saved", html[notice.Index..], StringComparison.Ordinal);
        Assert.False(rested.Shows);
        Assert.DoesNotContain("class=\"notice", EngineFixture.Body(rested.Page!), StringComparison.Ordinal);
        Assert.Single(NoticePattern().Matches(stepped));
        Assert.DoesNotContain("class=\"notice", EngineFixture.Body(closed.Page!), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "ENGINE-148")]
    public async Task APostedButtonRaisesItsClickBeforeItsCommandAsync()
    {
        const string Body = """
            <StackPanel>
              <m:ClickedButton Command="{Binding RunCommand}" Content="Go" />
              <m:ClickedButton Handles="True" Command="{Binding RunCommand}" Content="Stop" />
            </StackPanel>
            """;
        var going = new Clicking();
        var stopping = new Clicking();

        await fixture.PostAsync(Body, going, ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken);
        await fixture.PostAsync(Body, stopping, ("_btn", "1|")).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 1), (going.Clicks, going.Ran));
        Assert.Equal((1, 0), (stopping.Clicks, stopping.Ran));
    }

    private async Task<string> AskAsync()
    {
        var asked = await fixture.PostAsync(Confirm, new Confirming(), ("f0", "Ada"), ("_btn", "0|")).WaitAsync(TestContext.Current.CancellationToken);
        return EngineFixture.Body(asked.Page!, new Confirming());
    }

    private static string Prompt(string html) => WebUtility.HtmlDecode(PromptPattern().Match(html).Groups[1].Value);

    private static string PromptText(string html) => TextPattern().Match(html, html.IndexOf("<dialog", StringComparison.Ordinal)).Groups[1].Value;

    private static string Button(string html, string text, string? after = null)
    {
        var start = after is null ? 0 : html.IndexOf(after, StringComparison.Ordinal);
        return ButtonPattern().Matches(html, start).First(match => match.Groups[2].Value == text).Groups[1].Value;
    }

    private static (bool Listed, bool Prompted) Opened()
    {
        var drafts = new ObservableCollection<string>();
        var host = new Panel();
        var window = new Window { Content = new StackPanel { Children = { new ItemsControl { ItemsSource = drafts }, host } } };
        window.ApplyStyling();
        Realization.Realize(window);
        var prompts = Prompts.Watch(window);
        drafts.Add("draft");
        Realization.Realize(window);
        var listed = prompts.Opened();
        host.Children.Add(new Border());
        var prompted = prompts.Opened();
        window.Close();
        return (listed, prompted);
    }

    [GeneratedRegex("name=\"_prompt\" value=\"([^\"]*)\"")]
    private static partial Regex PromptPattern();

    [GeneratedRegex("<input type=\"hidden\" name=\"_answers\" value=\"([^\"]*)\">")]
    private static partial Regex AnswersPattern();

    [GeneratedRegex("<input type=\"text\" name=\"(f[^\"]+)\"")]
    private static partial Regex TextPattern();

    [GeneratedRegex("name=\"_btn\" value=\"([^\"]*)\"[^>]*>(?:<[^>]+>)*([^<]*)<")]
    private static partial Regex ButtonPattern();

    [GeneratedRegex("<div popover=\"manual\" class=\"notice[^>]*>")]
    private static partial Regex NoticePattern();

    [GeneratedRegex("position-anchor:--([a-z0-9]+)")]
    private static partial Regex AnchorPattern();
}
