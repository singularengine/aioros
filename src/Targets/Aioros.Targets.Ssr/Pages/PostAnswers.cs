using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Answers a post once it is read and allowed: binds and runs it through the page's plan or the engine, then answers
/// what it changed, the page, its refreshed views' regions, a redirect or a conflict.
/// </summary>
internal sealed class PostAnswers(PageHandler handler, IPageEngine engine, PlanCache cache, SsrOptions options, IFieldResolver? fields, PageAnswerWriter writer)
{

    /// <summary>
    /// Answers the post on <paramref name="held"/>, a live connection's models, or on models loaded for it, and returns the
    /// models as the answer left them, after the loaders it ran again. Held models navigate with the navigator of the scope
    /// they come from, <paramref name="scope"/>, opened on the post's page first so an earlier post's navigation is not
    /// followed again (SSR-290).
    /// </summary>
    public async Task<LoadedPage> AnswerAsync(HttpContext http, PageDefinition page, PostedValues posted, ImmutableDictionary<string, string> values, LoadedPage? held = null, IServiceProvider? scope = null)
    {
        using var locals = LocalValues.Use();
        var theme = ThemeSwitch.Of(http);
        var modelsScope = held is null ? http.RequestServices : scope ?? http.RequestServices;
        if (held is not null)
        {
            PageLoader.Navigator(modelsScope)?.Open(PageModels.Request(http).Path);
        }

        var loaded = held ?? await handler.LoadAsync(http, page, theme);
        if (loaded.Outcome is { Stops: true } && await handler.AnsweredAsync(http, PageLoader.Answer(http.RequestServices, loaded), StatusCodes.Status303SeeOther))
        {
            return loaded;
        }

        var models = loaded.Models;
        var view = ViewRegions.Commanded(page, values);
        var changes = view is not null ? DataKeys.Begin() : null;
        var (status, entry, shows) = await ApplyAsync(page, models, values, posted.Files, theme, http.RequestAborted);
        DataKeys.End();
        if (status == PostStatus.Rendered && PageModels.RedirectTo(modelsScope, models) is { } location)
        {
            PageRedirects.Redirect(http, StatusCodes.Status303SeeOther, location);
            return loaded;
        }

        if (status == PostStatus.Stale)
        {
            PageHandler.Stale(http);
            return loaded;
        }

        if (status != PostStatus.Rendered || entry is null)
        {
            http.Response.StatusCode = status == PostStatus.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            await writer.WriteAsync(http, page, theme, await handler.EntryAsync(page, models, theme, http.RequestAborted), loaded);
            return loaded;
        }

        if (shows)
        {
            await writer.WriteAsync(http, page, theme, entry, loaded, whole: true);
            return loaded;
        }

        return view is { } commanded
            ? await AnswerViewAsync(http, page, entry, loaded, theme, commanded, changes)
            : await AnswerPageAsync(http, page, entry, loaded, theme, values);
    }

    /// <summary>
    /// Answers a post that ran no view's command: a page or layout command revalidates the page; a post that changed only
    /// the slot region its field or button sits in gets that region alone for the script; a changed selection in a view,
    /// which runs no command, gets that view's region alone; anything else gets the whole page.
    /// </summary>
    private async Task<LoadedPage> AnswerPageAsync(HttpContext http, PageDefinition page, CachedPage entry, LoadedPage loaded, string theme, ImmutableDictionary<string, string> values)
    {
        if (ViewRegions.IsCommanded(values) && PageLoader.Revalidates(page, loaded.Models))
        {
            return await RevalidateAsync(http, page, entry, loaded, theme);
        }

        var scripted = entry.Page.Reusable && http.Request.Headers[DocumentParts.NavigationHeader] == "1";
        using var rendered = scripted && entry.Page.Plan.HasSlotRegions ? SlotAnswers.Render(http, entry, loaded) : null;
        if (rendered is not null && http.Request.Headers[SlotAnswers.HashesHeader].ToString() is { Length: > 0 } known && await SlotAnswers.WriteAsync(http, rendered, values, known))
        {
            return loaded;
        }

        if (scripted && ViewRegions.Changed(page, values) is { } region && await ViewRegions.WriteAsync(http, entry, loaded, page, [region]))
        {
            return loaded;
        }

        await writer.WriteAsync(http, page, theme, entry, loaded, rendered: rendered);
        return loaded;
    }

    private async Task<LoadedPage> RevalidateAsync(HttpContext http, PageDefinition page, CachedPage entry, LoadedPage loaded, string theme)
    {
        var fresh = await PageLoader.RevalidateAsync(http.RequestServices, page, PageModels.Request(http), loaded, http.RequestAborted);
        if (await handler.AnsweredAsync(http, PageLoader.Answer(http.RequestServices, fresh), StatusCodes.Status303SeeOther))
        {
            return fresh;
        }

        await writer.WriteAsync(http, page, theme, entry.Page.Reusable ? entry : await handler.EntryAsync(page, fresh.Models, theme, http.RequestAborted), fresh);
        return fresh;
    }

    /// <summary>
    /// Answers a command in a view: its own view's loader and those whose read keys it invalidated run again (all of them
    /// after a function without key information), and the navigation script gets those views' regions alone (Turbo
    /// Frames and Turbo Streams, LiveView's diff per component), while a plain form post, or a command that reached the
    /// page's or a layout's keys, gets the whole page. A command in one item of a repeated view refreshes that item alone,
    /// its loader running with that item's key, unless what it changed reaches the view's own keys.
    /// </summary>
    private async Task<LoadedPage> AnswerViewAsync(HttpContext http, PageDefinition page, CachedPage entry, LoadedPage loaded, string theme, ViewTarget view, DataKeys? changes)
    {
        var slots = PageKeys.Invalidated(page, view.Slot, loaded, changes);
        var item = view.Item is not null && !PageKeys.Reaches(loaded, view.Slot, changes) ? view.Item : null;
        var others = item is null ? slots : slots.Remove(view.Slot);
        var fresh = await RefreshAsync(http, page, loaded, view, item, others);
        if (fresh.Outcome is { Stops: true } && await handler.AnsweredAsync(http, PageLoader.Answer(http.RequestServices, fresh), StatusCodes.Status303SeeOther))
        {
            return fresh;
        }

        var current = entry.Page.Reusable || fresh == loaded ? entry : await handler.EntryAsync(page, fresh.Models, theme, http.RequestAborted);
        var regions = fresh == loaded || slots.IsDefaultOrEmpty ? [view] : ViewRegions.Of(page, fresh, others, item is null ? null : view);
        if (current.Page.Reusable && http.Request.Headers[DocumentParts.NavigationHeader] == "1" && !PageKeys.ReachesPage(page, others) && await ViewRegions.WriteAsync(http, current, fresh, page, regions))
        {
            return fresh;
        }

        await writer.WriteAsync(http, page, theme, current, fresh);
        return fresh;
    }

    private static async ValueTask<LoadedPage> RefreshAsync(HttpContext http, PageDefinition page, LoadedPage loaded, ViewTarget view, string? item, ImmutableArray<int> others)
    {
        if (!PageLoader.RevalidatesSlots(item is null ? others : [view.Slot], loaded.Models))
        {
            return loaded;
        }

        var request = PageModels.Request(http);
        var fresh = others.IsDefaultOrEmpty ? loaded : await PageLoader.RevalidateSlotsAsync(http.RequestServices, page, request, loaded, others, http.RequestAborted);
        return item is null || fresh.Outcome is { Stops: true } ? fresh : await PageLoader.RevalidateItemAsync(http.RequestServices, page, request, fresh, view.Slot, item, http.RequestAborted);
    }

    /// <summary>
    /// Applies a post through the page's plan when it can, otherwise through the engine, which also answers every post
    /// from an open prompt (only the engine replays the steps that opened it) and, while Avalonia runs in the process
    /// (<see cref="IPageEngine.RunsCommands"/>), every post pressing a button whose command the generator did not find
    /// runs on any thread (<see cref="PageDefinition.AnywhereCommands"/>), so the command runs on Avalonia's thread.
    /// </summary>
    private async ValueTask<(PostStatus Status, CachedPage? Entry, bool Shows)> ApplyAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken)
    {
        var engineThread = engine.RunsCommands && form.ContainsKey(PlanForms.ButtonField);
        if (PlanAnswers(page, form, engineThread) && cache.TryGet(page.Route, theme, out var cached)
            && Proven(page, cached, models) && await PlannedPostAsync(cached, models, form, engineThread ? command => command.Path is { } path && page.AnywhereCommands.Contains(path) : static _ => true) is { } status)
        {
            return (status, cached, false);
        }

        var planned = cache.TryGet(page.Route, theme, out var entry) && entry.Page.Reusable && files.IsDefaultOrEmpty && Proven(page, entry, models) && Describes(entry.Page.Plan, models, form) ? entry : null;
        var outcome = await engine.PostAsync(page, models, form, files, theme, planAnswers: planned is not null, cancellationToken);
        if (planned is not null && outcome.Page is null && fields is not null && !PlanForms.BindLocals(planned.Page.Plan, models, form, fields))
        {
            var answered = await engine.AnswerAsync(page, models, theme, cancellationToken);
            return (outcome.Status, new CachedPage(answered, DocumentParts.For(WithPlanCss(answered, page, theme), options, page.Live)), outcome.Shows);
        }

        return (outcome.Status, outcome.Page is { } compiled ? new CachedPage(compiled, DocumentParts.For(WithPlanCss(compiled, page, theme), options, page.Live)) : outcome.Status == PostStatus.Rendered ? planned : null, outcome.Shows);
    }

    /// <summary>Whether the plan may answer the post at all: not from an open prompt, on a field left to the engine, or pressing a command only Avalonia's thread runs.</summary>
    private bool PlanAnswers(PageDefinition page, ImmutableDictionary<string, string> form, bool engineThread) =>
        fields is not null && !form.ContainsKey(PlanForms.PromptField) && !LeftToEngine(page, form) && !(engineThread && page.ModelsUseAvalonia);

    /// <summary>
    /// Whether the page's verification proved every local input of the plan the guarantee does not cover
    /// (<see cref="PlanCache.Prove"/>): until it did, the engine answers the page's posts with its own render.
    /// </summary>
    private bool Proven(PageDefinition page, CachedPage cached, ImmutableArray<object?> models) =>
        !cached.Page.Plan.HasUnguaranteedLocals || PlanForms.Unguaranteed(cached.Page.Plan, models).All(cache.ProvenFields(page.Route).Contains);

    /// <summary>Whether the plan holds every travelling state the post carries for these models; not when it cannot write them.</summary>
    private static bool Describes(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form)
    {
        try
        {
            return PlanForms.DescribesStates(plan, models, form);
        }
        catch (PlanMissException)
        {
            return false;
        }
    }

    /// <summary>Whether the post changes a field whose plan answers otherwise than the engine (<see cref="PlanCache.LeaveToEngine"/>): named as the changed field, or pressed.</summary>
    private bool LeftToEngine(PageDefinition page, ImmutableDictionary<string, string> form) =>
        cache.EngineFields(page.Route) is { IsEmpty: false } left
        && ((form.TryGetValue(PlanForms.ChangeField, out var changed) && left.Contains("f" + changed)) || (form.TryGetValue(PlanForms.ButtonField, out var pressed) && left.Contains("_btn:" + pressed)));

    /// <summary>
    /// The engine's render with the CSS of the page's cached plan before its own rules the plan's does not hold: the engine writes the rules of what it
    /// rendered only, the browser takes an answer's stylesheet in place of the page's, and the plan keeps updating the
    /// page afterwards (slot answers, live updates) with classes whose rules only its CSS holds.
    /// </summary>
    private CompiledPage WithPlanCss(CompiledPage compiled, PageDefinition page, string theme) =>
        cache.TryGet(page.Route, theme, out var planned) && planned.Page.Reusable && !string.Equals(planned.Page.Css, compiled.Css, StringComparison.Ordinal)
            ? compiled with { Css = Documents.StyleRules.Join(planned.Page.Css, compiled.Css) }
            : compiled;

    /// <summary>A post bound and run through the cached plan's fields, or nothing when the plan does not describe them or cannot write these models.</summary>
    private async ValueTask<PostStatus?> PlannedPostAsync(CachedPage cached, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, Func<SlotSpec, bool> runs)
    {
        try
        {
            return await PlanForms.ApplyAsync(cached.Page.Plan, models, form, fields!, runs);
        }
        catch (PlanMissException)
        {
            return null;
        }
    }
}
