using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Pages;

public static class Warmup
{
    private static readonly Action<ILogger, string, Exception?> PageFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "WarmupPageFailed"), "{Route} could not be compiled at startup");

    private static readonly Action<ILogger, string, string, string?, Exception?> PageUnplanned =
        LoggerMessage.Define<string, string, string?>(LogLevel.Warning, new EventId(3, "WarmupPageUnplanned"), "{At}{Route} is answered by the engine: {Reason}");

    private static readonly Action<ILogger, string, string, string?, Exception?> FieldDiverges =
        LoggerMessage.Define<string, string, string?>(LogLevel.Warning, new EventId(4, "WarmupFieldDiverges"), "{Route}: the plan answers posts of {Field} otherwise than the engine, so the engine answers them: {Difference}");

    private static readonly Action<ILogger, string, string, Exception?> ListWithoutIdentity =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(5, "WarmupListWithoutIdentity"), "{Route}: {Warning}");

    private static readonly Action<ILogger, int, int, long, Exception?> Finished =
        LoggerMessage.Define<int, int, long>(LogLevel.Information, new EventId(2, "WarmupFinished"), "Warm-up finished: {Compiled} of {Total} pages compiled in {Elapsed} ms");

    /// <summary>
    /// Logs each page the warm-up could not compile and each it could not keep a plan for, then, once, that it finished: the signal a host, a startup probe or a
    /// test waits for before sending traffic, rather than racing the warm-up's own compilations.
    /// </summary>
    public static void Report(ILogger log, IReadOnlyList<WarmupOutcome> outcomes, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        foreach (var outcome in outcomes)
        {
            foreach (var field in outcome.Diverging.GroupBy(static verdict => verdict.Field.ToString()))
            {
                FieldDiverges(log, outcome.Route, field.Key, field.First().Difference, null);
            }
        }

        foreach (var outcome in outcomes.Where(static outcome => outcome.Error is not null))
        {
            PageFailed(log, outcome.Route, outcome.Error);
        }

        foreach (var (route, warning) in outcomes.SelectMany(static outcome => outcome.Warnings.Select(warning => (outcome.Route, warning))))
        {
            ListWithoutIdentity(log, route, warning, null);
        }

        foreach (var outcome in outcomes.Where(static outcome => outcome.Unplanned))
        {
            PageUnplanned(log, outcome.Located, outcome.Route, outcome.Reason, null);
        }

        Finished(log, outcomes.Count(static outcome => !outcome.Skipped && outcome.Error is null && outcome.Reason is null), outcomes.Count, (long)elapsed.TotalMilliseconds, null);
    }

    /// <summary>The pages the web renders, to precompile and warm: the app's pages but the native-only ones, then the special pages it has.</summary>
    public static ImmutableArray<PageDefinition> Planned(IEnumerable<PageDefinition> pages, params PageDefinition?[] special) =>
        [.. pages.Where(static page => !page.NativeOnly), .. special.OfType<PageDefinition>()];

    public static Task<IReadOnlyList<WarmupOutcome>> RunAsync(IEnumerable<PageDefinition> pages, IServiceProvider services, PageHandler handler, CancellationToken cancellationToken) =>
        RunAsync(pages, services, handler, posts: false, cancellationToken);

    /// <summary>
    /// Compiles the pages' plans and, with <paramref name="posts"/>, runs the post handling once on a post whose button names
    /// no field, which answers a conflict before anything is bound or run, so the first real post finds the post path
    /// compiled. The path is the framework's, the same for every page, so it is warmed on one: the page, not a special one,
    /// with the smallest plan whose fields the plan binds, which renders the least of the app's data.
    /// </summary>
    public static Task<IReadOnlyList<WarmupOutcome>> RunAsync(IEnumerable<PageDefinition> pages, IServiceProvider services, PageHandler handler, bool posts, CancellationToken cancellationToken) =>
        RunAsync(pages, services, handler, posts, verify: false, cancellationToken);

    /// <summary>
    /// Compiles each page's plan, and with <paramref name="verify"/> verifies its posts against the engine
    /// (<see cref="PlanParity"/>), leaving to the engine the fields whose plan answers otherwise and naming them in the
    /// page's outcome.
    /// </summary>
    public static async Task<IReadOnlyList<WarmupOutcome>> RunAsync(IEnumerable<PageDefinition> pages, IServiceProvider services, PageHandler handler, bool posts, bool verify, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(handler);
        var list = pages.ToList();
        var samples = await WarmupRequests.SamplesAsync(list, services, cancellationToken);
        var outcomes = new List<WarmupOutcome>();
        var requests = new List<(PageDefinition Page, PageRequest Request, int Outcome)>();
        foreach (var page in list)
        {
            var request = page.Route.Contains('{', StringComparison.Ordinal)
                ? samples.GetValueOrDefault(page.Route)
                : new PageRequest(page.Route, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty);
            if (request is null)
            {
                outcomes.Add(new WarmupOutcome(page.Route, [], "route parameters without a sample URL"));
                continue;
            }

            try
            {
                var outcome = await WarmAsync(page, request, services, handler, cancellationToken);
                outcomes.Add(verify && !outcome.Entries.IsDefaultOrEmpty ? await VerifyAsync(outcome, page, request, services, handler, cancellationToken) : outcome);
                requests.Add((page, request, outcomes.Count - 1));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                outcomes.Add(new WarmupOutcome(page.Route, [], error.Message, error));
            }
        }

        if (Hosting.LocalizedRequests.Supported(services) is [var first, ..])
        {
            PageCultures.Use(first.Culture, first.UICulture);
        }

        if (posts && await PostOnceAsync(requests, outcomes, services, handler, cancellationToken) is var (index, status))
        {
            outcomes[index] = outcomes[index] with { Posted = status };
        }

        return outcomes;
    }

    private static async Task<WarmupOutcome> VerifyAsync(WarmupOutcome outcome, PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, CancellationToken cancellationToken)
    {
        if (services.GetService(typeof(IPageEngine)) is not IPageEngine engine || services.GetService(typeof(IFieldResolver)) is not IFieldResolver resolver)
        {
            return outcome;
        }

        var result = await PlanParity.VerifyPageAsync(page, request, services, handler, engine, resolver, cancellationToken);
        var diverging = result.Verdicts.Where(static verdict => !verdict.Agrees).ToImmutableArray();
        if (!diverging.IsEmpty)
        {
            handler.Plans.LeaveToEngine(page.Route, diverging.Select(static verdict => verdict.Field.Name == "_btn" ? "_btn:" + verdict.Sample : verdict.Field.Name));
        }

        var proof = result.Shared || result.Volatile ? new LocalProof([], []) : await LocalProofs.ProveAsync(page, request, services, handler, engine, resolver, cancellationToken);
        handler.Plans.Prove(page.Route, proof.Proven);
        return outcome with { Diverging = diverging, Verified = result.Verdicts.Length, Unverified = result.Unverified, SharedModels = result.Shared, Volatile = result.Volatile, Proof = proof };
    }

    private static async Task<(int Outcome, int Status)?> PostOnceAsync(List<(PageDefinition Page, PageRequest Request, int Outcome)> requests, List<WarmupOutcome> outcomes, IServiceProvider services, PageHandler handler, CancellationToken cancellationToken)
    {
        var planned = requests
            .Where(warmed => !warmed.Page.Route.StartsWith("/_", StringComparison.Ordinal) && !outcomes[warmed.Outcome].Entries.IsDefaultOrEmpty)
            .OrderBy(warmed => outcomes[warmed.Outcome].Entries[0].Entry.Page.Plan.Template.Length);
        foreach (var (page, request, outcome) in planned)
        {
            if (handler.PostsThroughPlan(page, ThemeSwitch.Light, outcomes[outcome].Models))
            {
                return (outcome, await WarmupRequests.PostAsync(page, request, services, handler, cancellationToken));
            }
        }

        return null;
    }

    /// <summary>
    /// Loads and compiles the page for both themes in every culture the app supports (<see cref="Hosting.LocalizedRequests.Supported"/>),
    /// each load and render in its culture, or once in the app's own culture when it does not localize; the outcome keeps
    /// the models of the first culture.
    /// </summary>
    private static async Task<WarmupOutcome> WarmAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, CancellationToken cancellationToken)
    {
        var cultures = Hosting.LocalizedRequests.Supported(services);
        var entries = ImmutableArray.CreateBuilder<WarmedPage>();
        ImmutableArray<object?> first = default;
        ImmutableArray<object?> firstFresh = [];
        foreach (var culture in cultures.IsEmpty ? [null] : cultures.Select(static culture => (RequestCulture?)culture))
        {
            var (models, fresh, stop) = await WarmInAsync(page, request, services, handler, culture, entries, cancellationToken);
            if (stop is not null)
            {
                return stop;
            }

            if (first.IsDefault)
            {
                first = models;
                firstFresh = fresh;
            }
        }

        return new WarmupOutcome(page.Route, entries.ToImmutable(), null) { Warnings = [.. entries.SelectMany(static entry => entry.Entry.Page.Warnings).Distinct(StringComparer.Ordinal)], Models = first, FreshModels = firstFresh };
    }

    /// <summary>
    /// Loads and compiles the page for both themes in the culture, or in the app's own one, each theme from models loaded
    /// for it, since a page's render may write into its models (a library's default written back through a two-way
    /// binding, ENGINE-268); then loads them once more, untouched, for the check that a fresh request's models render from
    /// the saved plan (PRODUCTION-31). The outcome that stops the warmup of the page, if any.
    /// </summary>
    private static async Task<(ImmutableArray<object?> Models, ImmutableArray<object?> Fresh, WarmupOutcome? Stop)> WarmInAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, RequestCulture? culture, ImmutableArray<WarmedPage>.Builder entries, CancellationToken cancellationToken)
    {
        if (culture is not null)
        {
            PageCultures.Use(culture.Culture, culture.UICulture);
        }

        var loaded = await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken);
        var answer = PageLoader.Answer(services, loaded);
        if (!answer.Renders)
        {
            return (default, default, new WarmupOutcome(page.Route, [], answer.RedirectTo is { } location ? "its model redirects to " + location : "its model is not found") { Skipped = true });
        }

        foreach (var theme in (string[])[ThemeSwitch.Light, ThemeSwitch.Dark])
        {
            var models = theme == ThemeSwitch.Light ? loaded.Models : (await handler.LoadAsync(services, page, request, theme, cancellationToken)).Models;
            var entry = await handler.EntryAsync(page, models, theme, cancellationToken);
            if (!entry.Page.Reusable)
            {
                return (default, default, new WarmupOutcome(page.Route, [], "not reusable: " + (string.IsNullOrWhiteSpace(entry.Page.Failure) ? "no reason was recorded" : entry.Page.Failure)) { Unplanned = true, At = entry.Page.FailureAt });
            }

            entries.Add(new WarmedPage(theme, entry) { Culture = PageCultures.Key });
        }

        var fresh = await handler.LoadAsync(services, page, request, ThemeSwitch.Light, cancellationToken);
        return (loaded.Models, fresh.Models, null);
    }
}

/// <summary>A page compiled at warmup for a theme and, in an app that localizes, a culture (<see cref="PageCultures.Key"/>).</summary>
public sealed record WarmedPage(string Theme, CachedPage Entry)
{
    public string Culture { get; init; } = string.Empty;
}

public sealed record WarmupOutcome(string Route, ImmutableArray<WarmedPage> Entries, string? Reason, Exception? Error = null)
{
    /// <summary>The page has nothing to precompile: its model only redirects or is not found.</summary>
    public bool Skipped { get; init; }

    /// <summary>What the author of the page should know about its plan, though it works (<c>AIOROS033</c>).</summary>
    public ImmutableArray<string> Warnings { get; init; } = [];

    /// <summary>The samples of the page's fields whose plan answered otherwise than the engine, when its posts were verified (<see cref="PlanParity"/>).</summary>
    public ImmutableArray<FieldVerdict> Diverging { get; init; } = [];

    /// <summary>How many samples of the page's fields were verified against the engine.</summary>
    public int Verified { get; init; }

    /// <summary>How many of the page's fields were left over past the verification's budget (<see cref="PlanParity.Budget"/>).</summary>
    public int Unverified { get; init; }

    /// <summary>Whether the page was not verified since its models are shared by the app.</summary>
    public bool SharedModels { get; init; }

    /// <summary>Whether the page was not verified since two loads of it render differently with no post.</summary>
    public bool Volatile { get; init; }

    /// <summary>The page compiled but its plan could not be kept (<see cref="Reason"/>), so the engine answers its requests.</summary>
    public bool Unplanned { get; init; }

    /// <summary>Where in the page's XAML the element that kept its plan is, when it is known.</summary>
    public SourcePosition? At { get; init; }

    /// <summary>The position, as a diagnostic leads with it (<c>Pages/lab/Page.axaml(12,6): </c>), or nothing.</summary>
    public string Located => At is null ? string.Empty : At + ": ";

    /// <summary>The status the page's post handling answered at startup with a button naming no field, or nothing for every other page.</summary>
    public int? Posted { get; init; }

    /// <summary>The proof of the page's local inputs outside the guarantee (<see cref="LocalProofs"/>): those proven, and each value that disagreed.</summary>
    public LocalProof Proof { get; init; } = new([], []);

    /// <summary>The models the page was compiled with.</summary>
    public ImmutableArray<object?> Models { get; init; } = [];

    /// <summary>The page's models loaded once more after its compiles, untouched by any render, as a request loads them.</summary>
    public ImmutableArray<object?> FreshModels { get; init; } = [];
}
