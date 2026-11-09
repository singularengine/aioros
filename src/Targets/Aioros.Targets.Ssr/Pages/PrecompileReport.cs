using System.Collections.Immutable;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// What precompilation reports: every page compiles to a plan, or the build stops with an error that says why, instead
/// of serving the page through the UI framework (Next.js fails a static export the same way on a dynamic route).
/// </summary>
public sealed record PrecompileReport(string Text, ImmutableArray<string> Errors)
{
    public const string Code = "AIOROS010";

    private const string Advice = "Every page must compile to a plan: change what the reason names, and list a sample URL for a route with parameters in an IPrecompileSource (like Next.js generateStaticParams).";

    private ImmutableArray<string> Planned { get; init; } = [];

    private int Total { get; init; }

    private string Body { get; init; } = string.Empty;

    public bool Failed => !Errors.IsEmpty;

    public static PrecompileReport Of(IReadOnlyList<WarmupOutcome> outcomes, IEnumerable<PlansFileSkip> skipped)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var unsaved = skipped.ToLookup(skip => skip.Route, StringComparer.Ordinal);
        var lines = new List<string>();
        var errors = ImmutableArray.CreateBuilder<string>();
        var planned = ImmutableArray.CreateBuilder<string>();
        foreach (var outcome in outcomes)
        {
            var reason = outcome switch
            {
                { Skipped: true } => null,
                { Error: { } error } => Describe(error),
                { Reason: { } failure } => string.IsNullOrWhiteSpace(failure) ? "no reason was recorded" : failure,
                _ when unsaved[outcome.Route].FirstOrDefault() is { } skip => "not serializable: " + skip.Reason,
                _ => null,
            };
            if (outcome.Skipped)
            {
                lines.Add($"  skipped      {outcome.Route}  ({outcome.Reason})");
            }
            else if (reason is null)
            {
                planned.Add(outcome.Route);
                lines.Add($"  precompiled  {outcome.Route}" + Verdict(outcome));
                lines.AddRange(outcome.Warnings.Select(warning => $"  warning {warning} ({outcome.Route})"));
            }
            else
            {
                var error = $"{outcome.Located}error {Code}: {outcome.Route}: {reason}";
                errors.Add(error);
                lines.Add("  " + error);
            }
        }

        var cultures = outcomes.SelectMany(static outcome => outcome.Entries.IsDefault ? [] : outcome.Entries).Select(static entry => entry.Culture).Where(static culture => culture.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var body = (cultures.Count == 0 ? string.Empty : "Cultures: " + string.Join(", ", cultures) + "\n") + string.Join('\n', lines) + "\n" + (errors.Count == 0 ? string.Empty : Advice + "\n");
        return new PrecompileReport(Header(planned.Count, outcomes.Count) + body, errors.ToImmutable()) { Planned = planned.ToImmutable(), Total = outcomes.Count, Body = body };
    }

    /// <summary>The fields of a verified page whose posts the engine answers, since the plan answered them otherwise (<see cref="PlanParity"/>), or nothing.</summary>
    private static string Verdict(WarmupOutcome outcome) =>
        (outcome.Diverging.IsDefaultOrEmpty
            ? string.Empty
            : "  (the engine answers posts of " + string.Join(", ", outcome.Diverging.Select(static verdict => verdict.Field.ToString()).Distinct(StringComparer.Ordinal)) + ": the plan answers them otherwise; first difference: " + outcome.Diverging[0].Difference + ")")
        + (outcome.Unverified > 0 ? $"  ({outcome.Unverified} fields not verified: past the verification's budget)" : string.Empty)
        + (outcome.SharedModels ? "  (posts not verified: its models are shared by the app)" : string.Empty)
        + (outcome.Volatile ? "  (posts not verified: its render changes from one load to the next)" : string.Empty)
        + Proof(outcome.Proof);

    /// <summary>The page's inputs outside the guarantee its verification proved, and those whose posts the engine answers with the first value each disagreed on.</summary>
    private static string Proof(LocalProof proof) =>
        (proof.Proven.IsDefaultOrEmpty ? string.Empty : $"  ({proof.Proven.Length} inputs outside the guarantee proven over their domains: {string.Join(", ", proof.Proven)})")
        + (proof.Failures.IsDefaultOrEmpty
            ? string.Empty
            : "  (the engine answers the page's posts: inputs outside the guarantee the plan renders otherwise: "
                + string.Join("; ", proof.Failures.GroupBy(static failure => failure.Field).Select(static group => group.First()).Select(static failure => $"{failure.Field} ({failure.Kind}) {failure.Value ?? "(none)"} in {failure.Theme}: {failure.Reason}")) + ")");

    /// <summary>
    /// The report with the check that each page renders the same bytes from plans read with generated accessors and with
    /// runtime delegates, the check that each renders from its saved plan for models loaded anew (PRODUCTION-31), and with
    /// how many member reads the generated accessors served. A page that differs or misses is no longer counted as precompiled.
    /// </summary>
    public PrecompileReport Checked(IReadOnlyCollection<PlanCheck> checks, AccessorCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(coverage);
        var differing = checks.Where(check => check.Difference is not null || check.Miss is not null).ToList();
        var differences = checks.Where(check => check.Difference is not null)
            .Select(check => $"error {Code}: {check.Route} ({check.Theme}): generated accessors and runtime delegates render different bytes from offset {check.Difference}")
            .ToImmutableArray();
        var misses = checks.Where(check => check.Miss is not null)
            .Select(check => $"error {Code}: {check.Route} ({check.Theme}): a fresh model misses its plan: {check.Miss}")
            .ToImmutableArray();
        var same = checks.Count - differences.Length;
        var planned = Planned.Count(route => differing.All(check => !string.Equals(check.Route, route, StringComparison.Ordinal)));
        var runtime = coverage.Runtime.IsEmpty ? string.Empty : "; read at run time: " + string.Join(", ", coverage.Runtime);
        var text = Header(planned, Total) + Body
            + $"Identical output: {same} of {checks.Count} pages and themes render the same bytes with generated accessors and with runtime delegates\n"
            + string.Concat(differences.Select(error => "  " + error + "\n"))
            + $"Fresh models: {checks.Count - misses.Length} of {checks.Count} pages and themes render from their saved plan for models loaded anew\n"
            + string.Concat(misses.Select(error => "  " + error + "\n"))
            + $"Generated accessors served {coverage.Generated} of {coverage.Read} member reads and writes ({coverage.GeneratedWrites} of {coverage.Writes} writes){runtime}\n";
        return this with { Text = text, Errors = Errors.AddRange(differences).AddRange(misses) };
    }

    /// <summary>The report with the check that every page's models load as production loads them, before Avalonia starts (<see cref="ProductionModels"/>).</summary>
    public PrecompileReport WithModels(ModelCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return this with { Text = Text + check.Text, Errors = Errors.AddRange(check.Errors) };
    }

    private static string Header(int planned, int total) => $"Precompiled {planned} of {total} pages\n";

    /// <summary>The innermost exception's type and message, past the wrappers that say nothing of their own.</summary>
    private static string Describe(Exception error)
    {
        var cause = error.GetBaseException();
        return string.IsNullOrWhiteSpace(cause.Message) ? cause.GetType().Name : $"{cause.GetType().Name}: {cause.Message}";
    }
}
