using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Testing.Domains;

namespace Aioros.Avalonia.Tests.Fuzzing;

public sealed class GeneratedPageTests(EngineFixture fixture)
{
    private readonly Dictionary<string, bool> _reductions = new(StringComparer.Ordinal);

    [Fact]
    [Trait("AC", "ENGINE-327")]
    public async Task NoGeneratedPageUnderTheGuaranteeDisagreesWithAvaloniaAsync()
    {
        var first = Setting("FUZZ_SEED", 1);
        var count = Setting("FUZZ_COUNT", 40);
        var outcomes = new List<(int Seed, string Outcome, string Body, string? Reduced, string? Failure)>();
        foreach (var seed in Enumerable.Range(first, count))
        {
            var body = XamlPages.Generate(seed);
            var (outcome, failure) = await OutcomeAsync(body);
            var reduced = outcome is "outside" or "bug" ? await XamlReduction.ReduceAsync(body, DisagreesAsync) : null;
            outcomes.Add((seed, reduced is not null && outcome == "outside" && await GuaranteedAsync(reduced) ? "bug" : outcome, body, reduced, failure));
        }

        await WriteAsync(outcomes, "FUZZ_OUT");
        var bugs = outcomes.Where(static entry => entry.Outcome == "bug").ToList();
        Assert.True(bugs.Count == 0, string.Join("\n\n", bugs.Select(static bug => $"seed {bug.Seed}: {bug.Failure}\n  reduced: {bug.Reduced}")));
    }

    [Fact]
    [Trait("AC", "ENGINE-331")]
    public async Task NoValidPageFromAvaloniasMetadataUnderTheGuaranteeDisagreesWithAvaloniaAsync()
    {
        var first = Setting("META_SEED", 1);
        var count = Setting("META_COUNT", 30);
        var outcomes = new List<(int Seed, string Outcome, string Body, string? Reduced, string? Failure)>();
        foreach (var seed in Enumerable.Range(first, count))
        {
            var body = AvaloniaPages.Generate(seed);
            if (!await ValidAsync(body))
            {
                outcomes.Add((seed, "invalid", body, null, null));
                continue;
            }

            var (outcome, failure) = await CrashOrOutcomeAsync(body);
            var reduced = outcome is "outside" or "bug" ? await XamlReduction.ReduceAsync(body, DisagreesAsync) : null;
            outcomes.Add((seed, reduced is not null && outcome == "outside" && await GuaranteedAsync(reduced) ? "bug" : outcome, body, reduced, failure));
        }

        await WriteAsync(outcomes, "META_OUT");
        var bugs = outcomes.Where(static entry => entry.Outcome is "bug" or "crash").ToList();
        Assert.True(bugs.Count == 0, string.Join("\n\n", bugs.Select(static bug => $"seed {bug.Seed} {bug.Outcome}: {bug.Failure}\n  page: {bug.Reduced ?? bug.Body}")));
    }

    /// <summary>Whether Avalonia loads and renders the page on its own (a capture of its pixels, no plan): a page it rejects says nothing of Aioros.</summary>
    private async Task<bool> ValidAsync(string body)
    {
        try
        {
            await fixture.Engine.CaptureXamlAsync(ControlHarness.Xaml(body, nameof(Profile)), [new Profile()], new CaptureOptions("light", 800, 600), TestContext.Current.CancellationToken);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The page's outcome, or a crash when Aioros fails on a page Avalonia renders.</summary>
    private async Task<(string Outcome, string? Failure)> CrashOrOutcomeAsync(string body)
    {
        try
        {
            return await OutcomeAsync(body);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return ("crash", error.GetType().Name + ": " + error.Message);
        }
    }

    [Fact]
    [Trait("AC", "ENGINE-328")]
    public async Task AFailingPageIsReducedToTheSmallestPageThatStillFailsAsync()
    {
        const string Page = """
            <StackPanel Spacing="8">
              <StackPanel.Styles>
                <Style Selector="TextBox"><Setter Property="Margin" Value="6" /></Style>
                <Style Selector="TextBox[Text=x]"><Setter Property="FontWeight" Value="Bold" /></Style>
                <Style Selector="Slider:pointerover"><Setter Property="Opacity" Value="0.6" /></Style>
              </StackPanel.Styles>
              <TextBox Name="i0" Text="demo" Width="200" AutomationProperties.Name="Text" />
              <CheckBox Name="i1" Content="Remember" Margin="4" />
              <Slider Name="i2" Minimum="0" Maximum="10" Value="4" Width="200" AutomationProperties.Name="Level" />
              <ToggleButton Name="i3" Content="Bold" />
            </StackPanel>
            """;

        var reduced = await XamlReduction.ReduceAsync(Page, DisagreesAsync);

        Assert.True(await DisagreesAsync(reduced));
        Assert.Equal("""<StackPanel><StackPanel.Styles><Style Selector="TextBox[Text=x]"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles><TextBox /></StackPanel>""", reduced);
    }

    /// <summary>A refusal without what names one page's controls (quoted values, numbers), so refusals of one cause count together.</summary>
    private static string Reason(string? failure) =>
        System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(failure ?? string.Empty, "'[^']*'", "'…'"), "[0-9]+", "#");

    private static int Setting(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private async Task<(string Outcome, string? Failure)> OutcomeAsync(string body)
    {
        var failures = await WalkAsync(body);
        if (failures.IsEmpty)
        {
            return ("closed", null);
        }

        if (failures is ["holds no local input"])
        {
            return ("no inputs", null);
        }

        if (failures is [var refused] && refused.StartsWith("not reusable: ", StringComparison.Ordinal))
        {
            return ("refused", refused);
        }

        if (!failures.Any(Disagreement))
        {
            return (failures.Any(static failure => failure.Contains("the plan answers nothing", StringComparison.Ordinal)) ? "declined" : "unwalked", failures[0]);
        }

        return (await GuaranteedAsync(body) ? "bug" : "outside", failures.First(Disagreement));
    }

    /// <summary>Whether every local input of the page is under the guarantee, so a disagreement of it is a bug rather than a gap its page's verification leaves to the engine.</summary>
    private async Task<bool> GuaranteedAsync(string body)
    {
        var page = await fixture.Engine.RenderXamlAsync(ControlHarness.Xaml(body, nameof(Profile)), [new Profile()], "light", TestContext.Current.CancellationToken);
        return !PlanForms.Fields(page.Plan, [new Profile()]).Fields.Any(static field => field.Field?.Path is { } path && LocalPaths.TryRead(path, out var local) && !local.Guaranteed);
    }

    /// <summary>Whether a failure says the plan and Avalonia render a value otherwise, not that the page could not be walked (a domain of one value).</summary>
    private static bool Disagreement(string failure) =>
        failure.Contains(": differs at ", StringComparison.Ordinal) || (failure.Contains("answers ", StringComparison.Ordinal) && !failure.Contains("the plan answers nothing", StringComparison.Ordinal));

    private async Task<bool> DisagreesAsync(string body)
    {
        if (_reductions.TryGetValue(body, out var known))
        {
            return known;
        }

        bool disagrees;
        try
        {
            disagrees = await LocalDomains.FirstDisagreementAsync(fixture.Engine, ControlHarness.Xaml(body, nameof(Profile)), static () => [new Profile()], "light", TestContext.Current.CancellationToken) is not null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            disagrees = false;
        }

        _reductions[body] = disagrees;
        return disagrees;
    }

    private async Task<ImmutableArray<string>> WalkAsync(string body) =>
        await LocalDomains.DisagreementsAsync(fixture.Engine, ControlHarness.Xaml(body, nameof(Profile)), static () => [new Profile()], "light", TestContext.Current.CancellationToken);

    private static async Task WriteAsync(List<(int Seed, string Outcome, string Body, string? Reduced, string? Failure)> outcomes, string setting)
    {
        if (Environment.GetEnvironmentVariable(setting) is not { Length: > 0 } path)
        {
            return;
        }

        var lines = new List<string> { $"{outcomes.Count} pages: " + string.Join(", ", outcomes.GroupBy(static entry => entry.Outcome).OrderByDescending(static group => group.Count()).Select(static group => $"{group.Count()} {group.Key}")) };
        foreach (var group in outcomes.Where(static entry => entry.Reduced is not null).GroupBy(static entry => entry.Reduced!).OrderByDescending(static group => group.Count()))
        {
            lines.Add($"\n{group.Count()}x {group.First().Outcome} (seeds {string.Join(", ", group.Select(static entry => entry.Seed).Take(8))}): {group.Key}\n  {group.First().Failure?[..Math.Min(260, group.First().Failure!.Length)]}");
        }

        lines.AddRange(outcomes.Where(static entry => entry.Outcome == "refused").GroupBy(static entry => Reason(entry.Failure)).OrderByDescending(static group => group.Count()).Select(static group => $"\nrefused {group.Count()}x: {group.Key}"));
        lines.AddRange(outcomes.Where(static entry => entry.Outcome == "crash").Select(static entry => $"\ncrash (seed {entry.Seed}): {entry.Failure}\n  page: {entry.Body}"));
        await File.WriteAllLinesAsync(path, lines, TestContext.Current.CancellationToken);
        var findings = outcomes.Where(static entry => entry.Outcome is "bug" or "crash")
            .GroupBy(static entry => entry.Reduced ?? entry.Body)
            .Select(static group => new Finding(group.First().Outcome, [.. group.Select(static entry => entry.Seed)], group.Key, group.First().Failure));
        await File.WriteAllTextAsync(path + ".json", System.Text.Json.JsonSerializer.Serialize(findings, FindingJson), TestContext.Current.CancellationToken);
    }

    private static readonly System.Text.Json.JsonSerializerOptions FindingJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>A bug or crash the generators found, by the smallest page that shows it, for the nightly run to file as an issue.</summary>
    private sealed record Finding(string Outcome, int[] Seeds, string Page, string? Failure);
}
