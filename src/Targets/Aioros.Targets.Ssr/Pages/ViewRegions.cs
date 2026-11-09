using System.Buffers;
using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The regions of a page's views that an answer carries alone (Turbo Frames and Turbo Streams, LiveView's diff per
/// component): which view a post commanded or changed, which regions a refresh reaches, and their HTML from the plan.
/// </summary>
internal static class ViewRegions
{
    /// <summary>
    /// The regions an answer carries, in document order: each refreshed view's, every shown item's of a repeated view that
    /// refreshed whole, and the commanded item's when it refreshed alone.
    /// </summary>
    public static ImmutableArray<ViewTarget> Of(PageDefinition page, LoadedPage fresh, ImmutableArray<int> slots, ViewTarget? item)
    {
        var regions = ImmutableArray.CreateBuilder<ViewTarget>();
        foreach (var slot in item is { } own ? [.. slots.Append(own.Slot).Distinct().Order()] : slots)
        {
            if (item is { } single && single.Slot == slot)
            {
                regions.Add(single);
            }
            else if (fresh.Models[slot] is ViewItems items)
            {
                regions.AddRange(items.Items.Where(static shown => shown.Model is not ViewState { Name: "hidden" }).Select(shown => new ViewTarget(slot, shown.Key)));
            }
            else if (!PageLoader.Repeated(page, slot))
            {
                regions.Add(new ViewTarget(slot));
            }
        }

        return regions.ToImmutable();
    }

    public static async ValueTask<bool> WriteAsync(HttpContext http, CachedPage entry, LoadedPage loaded, PageDefinition page, ImmutableArray<ViewTarget> views)
    {
        using var region = new PooledBuffer();
        var styles = new PageStyles();
        if (!Render(region, entry, loaded, views, http.Request.Path.Value + http.Request.QueryString.Value, styles))
        {
            return false;
        }

        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers[PageHandler.ViewHeader] = string.Join(", ", views.Select(view => view.Item is null ? page.Views[view.Slot - page.FirstView].Key : ViewItems.RegionKey(page.Views[view.Slot - page.FirstView].Key, view.Item)));
        KeyedStyles.Write(http.Response.BodyWriter, styles, KeyedStyles.Held(http).Contains);
        await http.Response.BodyWriter.WriteAsync(region.WrittenMemory, http.RequestAborted);
        return true;
    }

    /// <summary>
    /// Writes the regions of the given views from the page's plan; <see langword="false"/> when one cannot be rendered alone,
    /// or the plan cannot write these models (<see cref="PlanMissException"/>), so the whole page is answered instead; the
    /// rule sets the regions' slot renderers contribute to the page are collected in <paramref name="styles"/>.
    /// </summary>
    public static bool Render(IBufferWriter<byte> output, CachedPage entry, LoadedPage loaded, ImmutableArray<ViewTarget> views, string path, PageStyles? styles = null)
    {
        try
        {
            return !views.IsDefaultOrEmpty && views.All(view => entry.Page.Plan.RenderView(output, loaded.Models, view.Slot, view.Item, path, styles));
        }
        catch (PlanMissException)
        {
            return false;
        }
    }

    /// <summary>The view whose button the form posted (and its item, in a repeated view), or nothing for the page's, its layouts' or no button.</summary>
    public static ViewTarget? Commanded(PageDefinition page, ImmutableDictionary<string, string> form) =>
        IsCommanded(form) ? ViewIn(page, form) : null;

    /// <summary>
    /// The view whose field changed in a post without a button: no command runs and no loader runs again, so the script
    /// gets that view's region alone, rendered from what the post bound.
    /// </summary>
    public static ViewTarget? Changed(PageDefinition page, ImmutableDictionary<string, string> form) =>
        form.TryGetValue(PlanForms.ChangeField, out var changed) && changed.Length > 0 ? ViewIn(page, form) : null;

    public static bool IsCommanded(ImmutableDictionary<string, string> form) =>
        form.TryGetValue(PlanForms.ButtonField, out var button) && button.Length > 0;

    private static ViewTarget? ViewIn(PageDefinition page, ImmutableDictionary<string, string> form) =>
        PlanForms.ViewOf(form) is { } view && view.Slot >= page.FirstView && view.Slot < page.ModelCount && (view.Item is null) != PageLoader.Repeated(page, view.Slot) ? view : null;
}
