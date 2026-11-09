using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Themes;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Verifies differentially that a page's plan answers its posts as the engine does (decision "Deterministic plan
/// coverage"): for each field the page writes, each sample of its input kind (<see cref="PostedField.Samples"/>) is posted
/// as the browser would post it, the whole form as written with that field changed and named as the one that changed,
/// through the plan into one load of the page's models and through the engine into another, and the two results are
/// compared through the plan's own render. Commands are not run (their effects reach past the models); a field the plan
/// does not bind, or binds otherwise than the engine, is named in a verdict that does not agree.
/// </summary>
public static partial class PlanParity
{
    /// <summary>How many samples a page's verification posts at most; the fields past them are counted as not verified (<see cref="ParityResult.Unverified"/>).</summary>
    public const int Budget = 30;

    public static async Task<ImmutableArray<FieldVerdict>> VerifyAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, IPageEngine engine, IFieldResolver resolver, CancellationToken cancellationToken) =>
        (await VerifyPageAsync(page, request, services, handler, engine, resolver, cancellationToken)).Verdicts;

    /// <summary>
    /// Verifies the page's fields within <see cref="Budget"/> samples: one sample of each field (two of a radio group, so
    /// a choice between two radios is posted), in the order the page writes them, and the count of the fields left over.
    /// </summary>
    public static async Task<ParityResult> VerifyPageAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, IPageEngine engine, IFieldResolver resolver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(engine);
        var models = (await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken)).Models;
        var again = (await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken)).Models;
        if (Shared(models, again))
        {
            return new ParityResult([], 0, Shared: true);
        }

        var plan = (await handler.EntryAsync(page, models, ThemeSwitch.Light, cancellationToken)).Page.Plan;
        var written = Render(plan, models, request.Path);
        if (Normal(written) != Normal(Render(plan, again, request.Path)))
        {
            return new ParityResult([], 0, Volatile: true);
        }

        var form = PostedForms.Of(written);
        var verdicts = ImmutableArray.CreateBuilder<FieldVerdict>();
        var fields = PostedForms.Fields(written).Where(static field => field.Name != "_btn" || field.Kind == "press").ToList();
        var verified = 0;
        foreach (var field in fields)
        {
            var samples = field.Samples().Take(field.Kind == "radio" ? 2 : 1).ToList();
            if (verdicts.Count + samples.Count > Budget)
            {
                break;
            }

            verified++;
            foreach (var sample in samples)
            {
                var posted = Post(form, field, sample);
                var planned = (await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken)).Models;
                var answered = (await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken)).Models;
                verdicts.Add(await HeldAsync(field, sample, async () =>
                {
                    var (status, mine) = await PlannedAsync(plan, planned, posted, resolver, request.Path);
                    var (outcome, theirs) = await AnsweredAsync(page, plan, answered, posted, engine, resolver, request.Path, cancellationToken);
                    return new FieldVerdict(field, sample, status is not null && status == outcome.Status && mine == theirs, status is null ? "the plan does not bind it: " + (PlanForms.Refusal(plan, models, posted, resolver) ?? "its command is left to the engine") : Difference(mine, theirs));
                }));
            }
        }

        return new ParityResult(verdicts.ToImmutable(), fields.Count - verified);
    }

    /// <summary>A sample's verdict, or one that does not agree when the plan does not hold a value the post shows (a value it learned others of), so the engine answers the field.</summary>
    private static async Task<FieldVerdict> HeldAsync(PostedField field, string? sample, Func<Task<FieldVerdict>> verdict)
    {
        try
        {
            return await verdict();
        }
        catch (PlanMissException miss)
        {
            return new FieldVerdict(field, sample, false, "the plan does not hold the value: " + miss.Message);
        }
    }

    /// <summary>The plan's answer to the post and its render, with the request's own state the post's local inputs are written into.</summary>
    private static async Task<(PostStatus? Status, string Html)> PlannedAsync(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> posted, IFieldResolver resolver, string path)
    {
        using var locals = LocalValues.Use();
        var status = await PlanForms.ApplyAsync(plan, models, posted, resolver, static _ => false);
        return (status, Normal(Render(plan, models, path)));
    }

    /// <summary>The engine's answer to the post, rendered through the plan with the local inputs as the engine's controls took them.</summary>
    private static async Task<(PostOutcome Outcome, string Html)> AnsweredAsync(PageDefinition page, RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> posted, IPageEngine engine, IFieldResolver resolver, string path, CancellationToken cancellationToken)
    {
        using var locals = LocalValues.Use();
        var outcome = await engine.PostAsync(page, models, posted, [], ThemeSwitch.Light, planAnswers: true, cancellationToken);
        PlanForms.BindLocals(plan, models, posted, resolver);
        return (outcome, Normal(Render(plan, models, path)));
    }

    /// <summary>Whether two loads of the page gave a model of the same instance, one the app shares, which a verification's posts would change for every visitor.</summary>
    private static bool Shared(ImmutableArray<object?> first, ImmutableArray<object?> second) =>
        first.Zip(second).Any(static pair => pair.First is not null && ReferenceEquals(pair.First, pair.Second));

    private static ImmutableDictionary<string, string> Post(ImmutableDictionary<string, string> form, PostedField field, string? sample)
    {
        var posted = field.Name == "_btn" ? form.SetItem("_btn", sample ?? string.Empty) : sample is null ? form.Remove(field.Name) : form.SetItem(field.Name, sample);
        return field.Name.StartsWith('f') ? posted.SetItem(PlanForms.ChangeField, field.Name[1..]) : posted;
    }

    internal static string Render(RenderPlan plan, ImmutableArray<object?> models, string path)
    {
        using var buffer = new PooledBuffer();
        plan.RenderBody(buffer, models, path);
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static string Normal(string html) => Sealed().Replace(html, "name=\"$1\" value=\"\"");

    internal static string? Difference(string mine, string theirs)
    {
        if (mine == theirs)
        {
            return null;
        }

        var at = 0;
        while (at < mine.Length && at < theirs.Length && mine[at] == theirs[at])
        {
            at++;
        }

        var from = Math.Max(0, at - 60);
        return "plan: …" + mine[from..Math.Min(mine.Length, at + 60)] + "… engine: …" + theirs[from..Math.Min(theirs.Length, at + 60)] + "…";
    }

    [System.Text.RegularExpressions.GeneratedRegex("name=\"(_s[0-9]+(?:~[0-9a-f]{24})?)\" value=\"[^\"]*\"", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Sealed();
}

/// <summary>
/// A page's verification: its samples' verdicts, how many of its fields were left over past the budget, and whether it
/// was skipped since its models are shared or since two loads of it render differently with no post (live data, a clock),
/// where a difference would say nothing about the post.
/// </summary>
public sealed record ParityResult(ImmutableArray<FieldVerdict> Verdicts, int Unverified, bool Shared = false, bool Volatile = false);

/// <summary>One sample of one field: whether the plan and the engine left the same page, and where they differ when they did not.</summary>
public sealed record FieldVerdict(PostedField Field, string? Sample, bool Agrees, string? Difference);
