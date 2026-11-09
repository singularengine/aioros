using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// A post the engine binds and runs on its thread: the page is realized, the form bound and its command run, then the
/// page is written again with the prompts or notices the command left open, or, when the page's plan answers and the
/// post left none, not written at all: the plan writes the answer from the models the command left.
/// </summary>
internal static class EnginePosts
{
    public static async Task<PostOutcome> PostAsync(Func<Control> compose, ImmutableArray<object?> models, PostedForm form, string theme, bool planAnswers = false)
    {
        var (window, root) = PageWindows.Mount(compose, theme);
        try
        {
            PageWindows.Settle(window);
            Realization.Realize(window);
            var prompts = Prompts.Watch(window);
            var status = await FormBinder.ApplyAsync(window, root, models, form, prompts);
            if (status != PostStatus.Rendered)
            {
                return new PostOutcome(status, null);
            }

            PageWindows.Settle(window);
            var shown = prompts.Shown();
            if (!shown.IsEmpty)
            {
                Realization.LayOut(window);
                shown = prompts.Shown();
            }

            if (planAnswers && shown.IsEmpty)
            {
                return new PostOutcome(status, null);
            }

            return new PostOutcome(status, HtmlCompiler.Compile(window, root, models, planning: false, overlays: shown)) { Shows = !shown.IsEmpty };
        }
        finally
        {
            PageWindows.Unmount(window, root);
        }
    }
}
