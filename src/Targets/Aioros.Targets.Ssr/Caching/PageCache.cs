using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Caching;

/// <summary>Which answer of a page a request asks for: the document, the navigation script's page, its page without the layouts, or the plan's dynamics.</summary>
public enum PageVariant
{
    Document,
    Navigation,
    Partial,
    Dynamics,
}

/// <summary>What a request for a cacheable page needs to keep its answer: the entry's key, the clock when its loaders started and how long it lives.</summary>
public readonly record struct PageStore(PageCache.Key Key, long Stamp, TimeSpan Lifetime);

/// <summary>
/// The answers of pages that do not depend on the user, kept whole, as Next.js's incremental static regeneration keeps a
/// route's HTML and payload until <c>revalidateTag</c> or <c>revalidate</c> ends them. A page is kept when every
/// loader of its layouts, the page and its views declares a lifetime (<c>[Cached]</c>, which the build refuses on a loader
/// that reads the user), it has no access rules and is not live; its entry is keyed by host, path and query and theme,
/// serves each answer variant once rendered, and is served while the shortest lifetime of its loaders lasts, none of the
/// data keys they read was invalidated since they started (<see cref="DataVersions"/>) and its plan is the current one.
/// A served answer skips the loaders and the render, and its tag answers <c>If-None-Match</c> with <c>304</c>. The
/// entries are one immutable state swapped with <see cref="ImmutableInterlocked"/>, bounded by their bytes: past the
/// limit the dead entries go first, then those that expire soonest, down to three quarters of it.
/// </summary>
public sealed class PageCache(DataVersions versions, TimeProvider time, long maxBytes = PageCache.DefaultMaxBytes)
{
    public const long DefaultMaxBytes = 64L * 1024 * 1024;

    private readonly long _maxBytes = maxBytes;

    private State _state = new(ImmutableDictionary<Key, Entry>.Empty, 0);

    public DataVersions Versions { get; } = versions;

    /// <summary>Whether the cache keeps anything: it is off when limited to no bytes.</summary>
    public bool Keeps => _maxBytes > 0;

    /// <summary>How many pages are kept.</summary>
    public int Count => Volatile.Read(ref _state).Entries.Count;

    /// <summary>How many bytes the kept answers hold.</summary>
    public long Bytes => Volatile.Read(ref _state).Bytes;

    /// <summary>
    /// How long a page may be kept: the shortest lifetime of its layouts', page's and views' loaders; none when one of them
    /// declares none (or the slot has no loader, so its view model could come from anywhere), the page or one of its views
    /// has access rules, or it is live.
    /// </summary>
    public static TimeSpan? Lifetime(PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Live || !page.Access.IsEmpty || page.Loader?.Lifetime is not { } shortest)
        {
            return null;
        }

        foreach (var layout in page.Layouts)
        {
            if (layout.Loader?.Lifetime is not { } lifetime)
            {
                return null;
            }

            shortest = lifetime < shortest ? lifetime : shortest;
        }

        foreach (var view in page.Views.Select(static instance => instance.View))
        {
            if (!view.Access.IsEmpty || view.Loader?.Lifetime is not { } lifetime)
            {
                return null;
            }

            shortest = lifetime < shortest ? lifetime : shortest;
        }

        return shortest;
    }

    /// <summary>The key of a request's answer: its host, path and query, theme, and the culture it renders in (<see cref="Runtime.Rendering.PageCultures.Key"/>).</summary>
    public static Key KeyOf(HttpRequest request, string theme)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Key(request.Host.Value ?? string.Empty, request.Path.Value + request.QueryString.Value, theme, Runtime.Rendering.PageCultures.Key);
    }

    /// <summary>The variant a request asks for, given the plan's fingerprint and the layouts' signature of a kept body.</summary>
    public static PageVariant VariantOf(HttpRequest request, CachedPage plan, string? signature)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Page.Reusable && request.Headers[PageHandler.PlanHeader] == plan.Page.Plan.Template.Fingerprint)
        {
            return PageVariant.Dynamics;
        }

        if (request.Headers[Documents.DocumentParts.NavigationHeader] != "1")
        {
            return PageVariant.Document;
        }

        return signature is not null && request.Headers[PageHandler.LayoutHeader] == signature ? PageVariant.Partial : PageVariant.Navigation;
    }

    /// <summary>
    /// Answers a request from its kept page when the entry is current for the plan the request would render with and holds
    /// the variant it asks for: <c>304</c> when <c>If-None-Match</c> names its tag, the kept bytes otherwise.
    /// </summary>
    public async ValueTask<bool> TryAnswerAsync(HttpContext http, Key key, CachedPage? plan)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (plan is null || !Volatile.Read(ref _state).Entries.TryGetValue(key, out var entry) || !ReferenceEquals(entry.Plan, plan) || !Live(entry, time.GetUtcNow()))
        {
            return false;
        }

        var variant = VariantOf(http.Request, plan, entry.Signature);
        if (entry.Answers.GetValueOrDefault(variant) is not { } answer)
        {
            return false;
        }

        var response = http.Response;
        response.Headers.Vary = answer.Vary;
        Themes.ThemeSwitch.AskForHint(http);
        Hosting.LocalizedRequests.Answer(http);
        var tag = PageFreshness.Tag(http.Request, answer.Hash);
        PageFreshness.Describe(response, PageKind.Public, tag);
        if (PageFreshness.NotModified(http.Request, tag))
        {
            PageFreshness.AnswerNotModified(response);
            return true;
        }

        response.ContentType = answer.ContentType;
        if (variant == PageVariant.Partial)
        {
            response.Headers[PageHandler.PartialHeader] = "1";
        }

        response.ContentLength = answer.Bytes.Length;
        await response.Body.WriteAsync(answer.Bytes, http.RequestAborted);
        return true;
    }

    /// <summary>
    /// Keeps a rendered variant of a page: in its current entry, or in a new one when the kept entry is gone or stale;
    /// nothing when the cache is limited to no bytes, which turns it off.
    /// </summary>
    public void Store(PageStore store, ImmutableArray<string> keys, CachedPage plan, PageVariant variant, string? signature, Answer answer)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(answer);
        if (!Keeps)
        {
            return;
        }

        var now = time.GetUtcNow();
        var fresh = new Entry(plan, store.Stamp, keys, now + store.Lifetime, signature, ImmutableDictionary<PageVariant, Answer>.Empty.Add(variant, answer));
        ImmutableInterlocked.Update(ref _state, static (state, kept) =>
        {
            var (cache, key, fresh, variant, answer, now) = kept;
            var entry = state.Entries.TryGetValue(key, out var current) && ReferenceEquals(current.Plan, fresh.Plan) && cache.Live(current, now)
                ? current with { Answers = current.Answers.SetItem(variant, answer) }
                : fresh;
            var bytes = state.Bytes - (current?.Size ?? 0) + entry.Size;
            var next = new State(state.Entries.SetItem(key, entry), bytes);
            return bytes > cache._maxBytes ? cache.Compact(next, key, now) : next;
        }, (Cache: this, store.Key, Fresh: fresh, Variant: variant, Answer: answer, Now: now));
    }

    /// <summary>Forgets every kept page.</summary>
    public void Clear() => ImmutableInterlocked.Update(ref _state, static _ => new State(ImmutableDictionary<Key, Entry>.Empty, 0));

    private bool Live(Entry entry, DateTimeOffset now) => now < entry.Expires && Versions.Current(entry.Stamp, entry.Keys);

    private State Compact(State state, Key added, DateTimeOffset now)
    {
        var kept = state.Entries.ToBuilder();
        var bytes = state.Bytes;
        foreach (var (key, entry) in state.Entries)
        {
            if (!key.Equals(added) && !Live(entry, now))
            {
                kept.Remove(key);
                bytes -= entry.Size;
            }
        }

        var target = _maxBytes * 3 / 4;
        foreach (var soonest in kept.Where(pair => !pair.Key.Equals(added)).OrderBy(static pair => pair.Value.Expires).ToList())
        {
            if (bytes <= target)
            {
                break;
            }

            kept.Remove(soonest.Key);
            bytes -= soonest.Value.Size;
        }

        return new State(kept.ToImmutable(), bytes);
    }

    /// <summary>A kept page's key: the host, the path and query, the theme and the culture.</summary>
    public readonly record struct Key(string Host, string Target, string Theme, string Culture = "");

    /// <summary>One kept answer: its bytes, their hash (the tag before the content codings), its type and its <c>Vary</c>.</summary>
    public sealed record Answer(byte[] Bytes, ulong Hash, string ContentType, string Vary);

    private sealed record Entry(CachedPage Plan, long Stamp, ImmutableArray<string> Keys, DateTimeOffset Expires, string? Signature, ImmutableDictionary<PageVariant, Answer> Answers)
    {
        public long Size => Answers.Values.Sum(static answer => (long)answer.Bytes.Length);
    }

    private sealed record State(ImmutableDictionary<Key, Entry> Entries, long Bytes);
}
