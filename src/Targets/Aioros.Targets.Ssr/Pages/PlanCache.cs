using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;

namespace Aioros.Targets.Ssr.Pages;

public sealed class PlanCache
{
    private ImmutableDictionary<(string Route, string Theme, string Culture), CachedPage> _pages = ImmutableDictionary<(string Route, string Theme, string Culture), CachedPage>.Empty;

    private ImmutableDictionary<(string Route, string Theme, string Culture), string?> _unplanned = ImmutableDictionary<(string Route, string Theme, string Culture), string?>.Empty;

    private ImmutableDictionary<string, ImmutableHashSet<string>> _engineFields = ImmutableDictionary<string, ImmutableHashSet<string>>.Empty;

    private ImmutableDictionary<string, ImmutableHashSet<string>> _provenFields = ImmutableDictionary<string, ImmutableHashSet<string>>.Empty;

    public int Count => _pages.Count;

    /// <summary>
    /// Leaves to the engine the posts that change these fields of the page, named as the browser posts them
    /// (<c>f3</c>, or <c>_btn</c> with a check menu item's value): fields whose plan answers otherwise than the engine
    /// (<see cref="PlanParity"/>).
    /// </summary>
    public void LeaveToEngine(string route, IEnumerable<string> fields) =>
        ImmutableInterlocked.AddOrUpdate(ref _engineFields, route, _ => [.. fields], (_, known) => known.Union(fields));

    /// <summary>The fields of the page whose posts the engine answers (<see cref="LeaveToEngine"/>).</summary>
    public ImmutableHashSet<string> EngineFields(string route) => _engineFields.GetValueOrDefault(route, []);

    /// <summary>
    /// Records that the page's verification walked the domains of these fields, local inputs the guarantee does not cover
    /// (<see cref="PlanForms.Unguaranteed"/>), and the plan rendered every value as the engine does (<see cref="PlanParity"/>).
    /// </summary>
    public void Prove(string route, IEnumerable<string> fields) =>
        ImmutableInterlocked.AddOrUpdate(ref _provenFields, route, _ => [.. fields], (_, known) => known.Union(fields));

    /// <summary>The fields of the page whose domains its verification proved (<see cref="Prove"/>).</summary>
    public ImmutableHashSet<string> ProvenFields(string route) => _provenFields.GetValueOrDefault(route, []);

    public Stylesheets Styles { get; } = new();

    public CachedPage Document(CompiledPage page, SsrOptions options, bool live)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CachedPage(page, DocumentParts.For(page, options, live, page.Reusable && options.ExternalCss ? Styles : null));
    }

    /// <summary>The page's plan for the theme in the culture the current flow renders in (<see cref="PageCultures.Key"/>).</summary>
    public bool TryGet(string route, string theme, out CachedPage page) => _pages.TryGetValue((route, theme, PageCultures.Key), out page!);

    public void Store(string route, string theme, CachedPage page) => Store(route, theme, PageCultures.Key, page);

    /// <summary>Keeps the page's plan for the theme and the culture with this key.</summary>
    public void Store(string route, string theme, string culture, CachedPage page) => ImmutableInterlocked.AddOrUpdate(ref _pages, (route, theme, culture), page, (_, _) => page);

    /// <summary>Whether compiling the page's plan failed for the theme in the current culture, and why, so later requests only render it.</summary>
    public bool Unplanned(string route, string theme, out string? failure) => _unplanned.TryGetValue((route, theme, PageCultures.Key), out failure);

    /// <summary>Remembers that the page's plan could not be compiled for the theme, until the plans are cleared.</summary>
    public void Unplan(string route, string theme, string? failure) => ImmutableInterlocked.AddOrUpdate(ref _unplanned, (route, theme, PageCultures.Key), failure, (_, _) => failure);

    public int Preload(IEnumerable<PlannedPage> pages, IEnumerable<PageDefinition> definitions, SsrOptions options)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var routes = definitions.ToDictionary(definition => definition.Route, StringComparer.Ordinal);
        var loaded = 0;
        foreach (var page in pages)
        {
            if (routes.TryGetValue(page.Route, out var definition))
            {
                Store(page.Route, page.Theme, page.Culture, Document(page.Page, options, definition.Live));
                if (!page.EngineFields.IsDefaultOrEmpty)
                {
                    LeaveToEngine(page.Route, page.EngineFields);
                }

                if (!page.ProvenFields.IsDefaultOrEmpty)
                {
                    Prove(page.Route, page.ProvenFields);
                }

                loaded++;
            }
        }

        return loaded;
    }

    /// <summary>The cached reusable plan whose template has this fingerprint.</summary>
    public RenderPlan? Template(string fingerprint) =>
        _pages.Values.Select(entry => entry.Page).Where(page => page.Reusable).Select(page => page.Plan).FirstOrDefault(plan => plan.Template.Fingerprint == fingerprint);

    /// <summary>The routes whose cached reusable plan has a template with this fingerprint.</summary>
    public ImmutableArray<string> Routes(string fingerprint) =>
        [.. _pages.Where(entry => entry.Value.Page.Reusable && entry.Value.Page.Plan.Template.Fingerprint == fingerprint).Select(entry => entry.Key.Route).Distinct(StringComparer.Ordinal)];

    public void Clear()
    {
        ImmutableInterlocked.Update(ref _pages, pages => pages.Clear());
        ImmutableInterlocked.Update(ref _unplanned, unplanned => unplanned.Clear());
    }
}

public sealed record CachedPage(CompiledPage Page, DocumentParts Parts);
