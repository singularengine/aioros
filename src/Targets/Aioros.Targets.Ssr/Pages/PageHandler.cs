using Microsoft.Extensions.DependencyInjection;
using Aioros.Targets.Ssr.Themes;
using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Live;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

public sealed class PageHandler
{
    private readonly IPageEngine _engine;
    private readonly PlanCache _cache;
    private readonly SsrOptions _options;
    private readonly IFieldResolver? _fields;
    private readonly CrossSite.CrossOriginProtection _protection;
    private readonly PageAnswerWriter _writer;
    private readonly PostAnswers _posts;

    public PageHandler(IPageEngine engine, PlanCache cache, SsrOptions options, IFieldResolver? fields = null, CrossSite.CrossOriginProtection? protection = null, Caching.PageCache? pages = null)
    {
        _engine = engine;
        _cache = cache;
        _options = options;
        _fields = fields;
        _protection = protection ?? CrossSite.CrossOriginProtection.Default;
        Pages = pages;
        _writer = new PageAnswerWriter(this, options);
        _posts = new PostAnswers(this, engine, cache, options, fields, _writer);
    }

    /// <summary>The pages' plans, and the fields whose posts the engine answers.</summary>
    public PlanCache Plans => _cache;

    /// <summary>The page cache, when the app keeps pages that do not depend on the user.</summary>
    public Caching.PageCache? Pages { get; }

    /// <summary>
    /// Answers a page: from the page cache when its loaders declared it the same for every visitor and a current answer is
    /// kept (<c>304</c> when the request already holds it), otherwise from its loaders and plan, keeping the answer when it
    /// may be kept. Every answer carries its tag and the <c>Cache-Control</c> of its kind.
    /// </summary>
    public async Task GetAsync(HttpContext http, PageDefinition page)
    {
        http.RequestServices.GetService<DraftScopes>()?.Apply(http);
        var theme = ThemeSwitch.Of(http);
        var lifetime = Caching.PageCache.Lifetime(page);
        var freshness = new AnswerFreshness(lifetime is null ? Caching.PageKind.Private : Caching.PageKind.Public);
        if (lifetime is { } kept && Pages is { Keeps: true } cachedPages && !_options.Antiforgery)
        {
            var key = Caching.PageCache.KeyOf(http.Request, theme);
            if (await cachedPages.TryAnswerAsync(http, key, _cache.TryGet(page.Route, theme, out var plan) ? plan : null))
            {
                return;
            }

            freshness = freshness with { Store = new Caching.PageStore(key, cachedPages.Versions.Clock, kept) };
        }

        var loaded = await LoadAsync(http, page, theme);
        if (await AnsweredAsync(http, PageLoader.Answer(http.RequestServices, loaded), StatusCodes.Status302Found))
        {
            return;
        }

        var entry = await EntryAsync(page, loaded.Models, theme, http.RequestAborted);
        await _writer.WriteAsync(http, page, theme, entry, loaded, freshness);
    }

    internal async ValueTask<bool> AnsweredAsync(HttpContext http, PageAnswer answer, int redirect)
    {
        if (answer.NotFound)
        {
            await NotFoundAsync(http);
            return true;
        }

        if (answer.RedirectTo is { } location)
        {
            PageRedirects.Redirect(http, redirect, location);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Answers the route of a page that leaves out the web with a stand-in, never a 404 (Expo Router's universal fallback):
    /// the app's <c>Pages/NativeOnly.axaml</c> with its own models under the page's title, or the built-in one. The page's
    /// loaders do not run, and the answer is never kept as the route's content.
    /// </summary>
    public async Task NativeOnlyAsync(HttpContext http, PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_options.NativeOnly is not { } standIn)
        {
            var heads = await PageModels.LoadAsync(http, NativeOnlyStandIn.Heads(page));
            await NativeOnlyStandIn.WriteAsync(http, PageLoader.Title(heads), PageLoader.Metadata(heads), _options);
            return;
        }

        var shown = standIn with { Head = page.Head ?? standIn.Head };
        var theme = ThemeSwitch.Of(http);
        var loaded = await LoadAsync(http, shown, theme);
        if (await AnsweredAsync(http, PageLoader.Answer(http.RequestServices, loaded), StatusCodes.Status302Found))
        {
            return;
        }

        var entry = await EntryAsync(shown, loaded.Models, theme, http.RequestAborted);
        await _writer.WriteAsync(http, shown, theme, entry, loaded);
    }

    public Task NotFoundAsync(HttpContext http) => SpecialAsync(http, StatusCodes.Status404NotFound, _options.NotFound);

    public Task ErrorAsync(HttpContext http) => SpecialAsync(http, StatusCodes.Status500InternalServerError, _options.Error);

    private async Task SpecialAsync(HttpContext http, int status, PageDefinition? special)
    {
        http.Response.StatusCode = status;
        if (special is not { } page)
        {
            return;
        }

        var theme = ThemeSwitch.Of(http);
        var loaded = await LoadAsync(http, page, theme);
        var entry = await EntryAsync(page, loaded.Models, theme, http.RequestAborted);
        await _writer.WriteAsync(http, page, theme, entry, loaded);
    }

    public async Task PostAsync(HttpContext http, PageDefinition page)
    {
        http.RequestServices.GetService<DraftScopes>()?.Apply(http);
        if (!_protection.Allows(http.Request, page.Route))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var posted = await PostedValues.ReadAsync(http, _options.UploadLimit, http.RequestAborted);
        if (posted is null)
        {
            http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        if (_options.Antiforgery && http.RequestServices.GetService(typeof(IAntiforgery)) is IAntiforgery antiforgery && !await antiforgery.IsRequestValidAsync(http))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (StateSeals.Open(posted.Values) is not { } opened)
        {
            Stale(http);
            return;
        }

        if (Live.Of(http, page) is { } connection && await connection.PostAsync(held =>
            {
                http.Response.Headers[LiveStates.KeptHeader] = "1";
                return _posts.AnswerAsync(http, page, posted, opened, held, connection.Services);
            }))
        {
            return;
        }

        await _posts.AnswerAsync(http, page, posted, opened);
    }

    internal static void Stale(HttpContext http)
    {
        var written = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
        var self = PageRedirects.IsLocal(written) ? written : "/";
        if (http.Request.Headers[DocumentParts.NavigationHeader] == "1")
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            http.Response.Headers[RedirectHeader] = self;
            return;
        }

        http.Response.StatusCode = StatusCodes.Status303SeeOther;
        http.Response.Headers.Location = self;
    }

    /// <summary>
    /// The page as the engine renders it for these models, kept out of the cache: what answers a request whose models the
    /// cached plan cannot write (<see cref="PlanMissException"/>), while the plan stays for the requests it can write.
    /// </summary>
    public async ValueTask<CachedPage> RenderedAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken)
    {
        var compiled = await _engine.AnswerAsync(page, models, theme, cancellationToken);
        return _cache.Document(compiled, _options, page.Live);
    }

    /// <summary>
    /// Loads the page's models where they are rendered: on the engine's thread when the engine renders the page (no plan
    /// is cached for the theme), so that the Avalonia objects a model builds belong to the thread that draws them, and on
    /// the request's thread when its plan renders it. A page whose models create Avalonia objects
    /// (<see cref="PageDefinition.ModelsUseAvalonia"/>) starts an engine that starts on demand first (<see cref="IEngineStart"/>):
    /// its objects need Avalonia's platform, and Avalonia's UI dispatcher must be the engine's before any other thread
    /// creates one.
    /// </summary>
    public ValueTask<LoadedPage> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, string theme, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.ModelsUseAvalonia && _engine is IEngineStart start
            ? StartedLoadAsync(start, services, page, request, theme, cancellationToken)
            : PageModels.LoadAsync(services, page, request, _cache.TryGet(page.Route, theme, out _) ? null : _engine, cancellationToken);
    }

    private async ValueTask<LoadedPage> StartedLoadAsync(IEngineStart start, IServiceProvider services, PageDefinition page, PageRequest request, string theme, CancellationToken cancellationToken)
    {
        await start.StartAsync(cancellationToken);
        return await PageModels.LoadAsync(services, page, request, _cache.TryGet(page.Route, theme, out _) ? null : _engine, cancellationToken);
    }

    internal ValueTask<LoadedPage> LoadAsync(HttpContext http, PageDefinition page, string theme) =>
        LoadAsync(http.RequestServices, page, PageModels.Request(http), theme, http.RequestAborted);

    /// <summary>The open live connections of the pages this handler answers.</summary>
    public LiveStates Live { get; } = new();

    /// <summary>Whether a post on the page in this theme is bound and run through its cached plan, never the engine.</summary>
    /// <summary>Whether a post pressing a button on the page runs through its plan: never while the engine owns commands (<see cref="IPageEngine.OwnsCommands"/>).</summary>
    public bool PostsThroughPlan(PageDefinition page, string theme, ImmutableArray<object?> models) =>
        _fields is not null && !_engine.OwnsCommands && _cache.TryGet(page.Route, theme, out var cached) && PlanForms.Binds(cached.Page.Plan, models, _fields);

    public string? CachedCss(PageDefinition page, string theme) => _cache.TryGet(page.Route, theme, out var cached) ? cached.Page.Css : null;

    public async ValueTask<CachedPage> EntryAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken)
    {
        if (_cache.TryGet(page.Route, theme, out var cached))
        {
            return cached;
        }

        if (_cache.Unplanned(page.Route, theme, out var failure))
        {
            return _cache.Document(await _engine.AnswerAsync(page, models, theme, cancellationToken) with { Failure = failure }, _options, page.Live);
        }

        var compiled = await _engine.RenderAsync(page, models, theme, cancellationToken);
        var entry = _cache.Document(compiled, _options, page.Live);
        if (compiled.Reusable)
        {
            _cache.Store(page.Route, theme, entry);
        }
        else
        {
            _cache.Unplan(page.Route, theme, compiled.Failure);
        }

        return entry;
    }

    public const string PlanHeader = "X-Aioros-Plan";

    public const string LayoutHeader = "X-Aioros-Layout";

    public const string PartialHeader = "X-Aioros-Partial";

    public const string RedirectHeader = "X-Aioros-Redirect";

    /// <summary>Marks an answer that holds one view's region alone, by its instance key.</summary>
    public const string ViewHeader = RemoteLoaderResults.ViewHeader;

    public const string TemplatePath = "/_aioros/plan/";

    /// <summary>
    /// Answers a plan's template, which never changes for its fingerprint, under the access rules of the pages it belongs to:
    /// to anyone when one of them is public, otherwise to a visitor one of them allows, kept in that browser alone.
    /// </summary>
    public static async Task TemplateAsync(HttpContext http, PlanCache cache, string fingerprint, IReadOnlyDictionary<string, ImmutableArray<PageAccess>> access)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(access);
        var rules = cache.Routes(fingerprint).Where(access.ContainsKey).Select(route => access[route]).ToList();
        if (rules.Count == 0 || cache.Template(fingerprint) is not { } plan)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var shared = rules.Exists(rule => rule.IsEmpty);
        if (!shared && await RefusedAsync(http, rules) is { } refused)
        {
            http.Response.StatusCode = refused;
            return;
        }

        http.Response.ContentType = "application/json";
        http.Response.Headers.CacheControl = shared ? "public, max-age=31536000, immutable" : "private, max-age=31536000, immutable";
        PlanTemplate.Write(http.Response.BodyWriter, plan.Body);
        await http.Response.BodyWriter.FlushAsync(http.RequestAborted);
    }

    private static async Task<int?> RefusedAsync(HttpContext http, List<ImmutableArray<PageAccess>> rules)
    {
        int? refused = null;
        foreach (var rule in rules)
        {
            refused = await Access.AccessRules.RefusedAsync(http, [.. rule.Select(static access => (access.Policy, access.Roles))]);
            if (refused is null)
            {
                return null;
            }
        }

        return refused;
    }
}
