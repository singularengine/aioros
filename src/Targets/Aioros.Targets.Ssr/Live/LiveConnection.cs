using System.Buffers;
using System.Collections.Immutable;
using System.Threading.Channels;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Live;

/// <summary>
/// One live page as one connection sees it: its models, the keys each of its loaders read, and what it has sent. An
/// invalidation reruns only the loaders that read its keys (<see cref="PageKeys.Reading"/>), each in a new scope with the
/// connection's own user and access rules, as Phoenix LiveView renders per connection; the answer is the refreshed views'
/// regions when nothing else changed, otherwise the page's body or, for a script that knows the plan, its dynamics.
/// </summary>
internal sealed class LiveConnection(HttpContext http, PageDefinition page, PageHandler handler, LoadedPage loaded) : IDisposable
{
    private readonly string? _known = handler.CachedCss(page, ThemeSwitch.Of(http));

    private readonly string _path = PagePath(http.Request.Path.Value) + PageQuery(http.Request.Query);

    private readonly PageRequest _request = PageModels.Request(http);

    private LoadedPage _loaded = loaded;

    private ImmutableArray<int> _refreshed = [];

    private string? _styles;

    private readonly Dictionary<string, string> _sent = [];

    private readonly Channel<LivePost> _posts = Channel.CreateUnbounded<LivePost>(new UnboundedChannelOptions { SingleReader = true });

    public LiveSignals Signals { get; } = Watched(loaded, page);

    /// <summary>Starts following the app's invalidations until <paramref name="cancellationToken"/> is cancelled.</summary>
    public Task FollowAsync(CancellationToken cancellationToken) =>
        http.RequestServices.GetService(typeof(DataVersions)) is DataVersions versions ? Signals.FollowAsync(versions.WatchAsync(cancellationToken)) : Task.CompletedTask;

    /// <summary>Reruns the loaders the pending invalidation reaches; <see langword="false"/> when one stopped the page.</summary>
    public async ValueTask<bool> RefreshAsync()
    {
        _refreshed = Signals.TakeInvalidation() is { } invalidation ? PageKeys.Reading(page, _loaded, invalidation) : [];
        if (_refreshed.IsEmpty)
        {
            return true;
        }

        _loaded = await PageLoader.RevalidateSlotsAsync(http.RequestServices, page, _request, _loaded, _refreshed, http.RequestAborted);
        if (_loaded.Outcome is { Stops: true })
        {
            return false;
        }

        Signals.Observe(_loaded.Models[page.Layouts.Length]);
        return true;
    }

    /// <summary>
    /// Runs <paramref name="answer"/> on this connection's models once the stream has done what it is doing, as the stream's
    /// own work; <see langword="false"/>, without running it, when the stream ended first.
    /// </summary>
    /// <summary>The scope the connection's models come from, whose navigator a post on them navigates with (SSR-290).</summary>
    public IServiceProvider Services => http.RequestServices;

    public async Task<bool> PostAsync(Func<LoadedPage, Task<LoadedPage>> answer)
    {
        var post = new LivePost(answer, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        if (!_posts.Writer.TryWrite(post))
        {
            return false;
        }

        Signals.Wake();
        return await post.Answered.Task;
    }

    /// <summary>Runs the posts handed to this connection, one at a time, each on the models the one before left.</summary>
    public async Task AnswerPostsAsync()
    {
        while (_posts.Reader.TryRead(out var post))
        {
            try
            {
                _loaded = await post.Answer(_loaded);
                Signals.Observe(_loaded.Models[page.Layouts.Length]);
                post.Answered.SetResult(true);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                post.Answered.SetException(exception);
            }
        }
    }

    /// <summary>
    /// Writes the event to send for what changed since the last one into <paramref name="message"/>; <see langword="false"/>
    /// when nothing did. The body is rendered into a buffer rented from the shared array pool, so a push grows no array.
    /// </summary>
    public async ValueTask<bool> MessageAsync(PooledBuffer message)
    {
        var changed = Signals.TakeChange();
        if (_refreshed.IsEmpty && !changed)
        {
            return false;
        }

        var entry = await handler.EntryAsync(page, _loaded.Models, ThemeSwitch.Of(http), http.RequestAborted);
        using var body = new PooledBuffer();
        try
        {
            Message(body, message, entry, changed);
        }
        catch (PlanMissException)
        {
            body.ResetWrittenCount();
            message.ResetWrittenCount();
            var styles = new PageStyles();
            Html(body, await handler.RenderedAsync(page, _loaded.Models, ThemeSwitch.Of(http), http.RequestAborted), styles);
            Event(message, body, styles);
        }

        return true;
    }

    /// <summary>
    /// The event of a body or of views' regions, preceded by the keyed rule sets the browser does not hold in their versions,
    /// which the connection then remembers as the ones it holds.
    /// </summary>
    private void Event(PooledBuffer message, PooledBuffer body, PageStyles styles, string? name = null)
    {
        using var styled = new PooledBuffer();
        if (!KeyedStyles.Write(styled, styles, Sent))
        {
            LiveUpdates.Event(message, body.WrittenSpan, name);
            return;
        }

        styled.Write(body.WrittenSpan);
        LiveUpdates.Event(message, styled.WrittenSpan, name);
    }

    /// <summary>
    /// Whether the browser holds the rule set in this version, the one of its key this connection sent last, remembering it as
    /// sent when it does not: the script keeps one version of a key, replacing the one it held.
    /// </summary>
    private bool Sent(string version)
    {
        var key = version[..version.LastIndexOf(':')];
        if (_sent.TryGetValue(key, out var held) && held == version)
        {
            return true;
        }

        _sent[key] = version;
        return false;
    }

    /// <summary>The event for what changed, from the page's plan: the refreshed views' regions, the page's dynamics, or its body.</summary>
    private void Message(PooledBuffer body, PooledBuffer message, CachedPage entry, bool changed)
    {
        var styles = new PageStyles();
        if (!changed && !PageKeys.ReachesPage(page, _refreshed) && entry.Page.Reusable
            && ViewRegions.Render(body, entry, _loaded, ViewRegions.Of(page, _loaded, _refreshed, null), _path, styles))
        {
            Event(message, body, styles, LiveUpdates.ViewsEvent);
            return;
        }

        body.ResetWrittenCount();
        styles = new PageStyles();
        if (entry.Page.Reusable && http.Request.Query["plan"] == entry.Page.Plan.Template.Fingerprint)
        {
            Dynamics(body, entry, styles);
            LiveUpdates.Event(message, body.WrittenSpan);
            return;
        }

        Html(body, entry, styles);
        Event(message, body, styles);
    }

    public void Dispose()
    {
        _posts.Writer.TryComplete();
        while (_posts.Reader.TryRead(out var post))
        {
            post.Answered.SetResult(false);
        }

        Signals.Dispose();
    }

    private static LiveSignals Watched(LoadedPage loaded, PageDefinition page)
    {
        var signals = new LiveSignals();
        signals.Observe(loaded.Models[page.Layouts.Length]);
        return signals;
    }

    private static string PageQuery(IQueryCollection query)
    {
        var page = new Microsoft.AspNetCore.Http.Extensions.QueryBuilder(query.Where(pair => pair.Key != "plan")).ToQueryString();
        return page.Value ?? string.Empty;
    }

    private static string PagePath(string? path) =>
        path is { } value && value.EndsWith("/live", StringComparison.Ordinal) ? (value.Length == 5 ? "/" : value[..^5]) : path ?? "/";

    private void Html(PooledBuffer body, CachedPage entry, PageStyles styles)
    {
        if (_styles is not null || (entry.Page.Css != _known && (_known is not null || !entry.Page.Reusable)))
        {
            _styles = entry.Page.Css;
            var output = new HtmlOutput(body);
            output.WriteUtf8("<style data-live-css>"u8);
            output.WriteRaw(_styles);
            output.WriteUtf8("</style>"u8);
        }

        entry.Page.Plan.RenderBody(body, _loaded.Models, _path, styles: styles);
    }

    private void Dynamics(PooledBuffer body, CachedPage entry, PageStyles styles)
    {
        using var dynamics = new PooledBuffer();
        entry.Page.Plan.WriteDynamics(dynamics, _loaded.Models, _path, styles);
        using var keyed = new PooledBuffer();
        var fresh = KeyedStyles.Write(keyed, styles, Sent);
        using var json = new System.Text.Json.Utf8JsonWriter(body, new System.Text.Json.JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        json.WriteStartObject();
        json.WriteString("t", PageLoader.Title(_loaded));
        json.WritePropertyName("d");
        json.WriteRawValue(dynamics.WrittenSpan, skipInputValidation: true);
        if (fresh)
        {
            json.WriteString("s", keyed.WrittenSpan);
        }

        json.WriteEndObject();
    }

    private sealed record LivePost(Func<LoadedPage, Task<LoadedPage>> Answer, TaskCompletionSource<bool> Answered);
}
