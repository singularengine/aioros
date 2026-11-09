using Aioros.Avalonia.Html.Templating;
using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html;

internal static class HtmlCompiler
{
    public static CompiledPage Compile(Window window, Control root, ImmutableArray<object?> models, bool planning, Control? viewError = null, Overlays? overlays = null, ImmutableDictionary<Control, string>? entrances = null) =>
        CompileWithFields(window, root, models, planning, viewError, overlays, entrances).Page;

    /// <summary>
    /// Writes the page, and, when planning a page with views, first the view error template (shown and styled in a window
    /// of its own) as the error body of its views.
    /// </summary>
    public static (CompiledPage Page, FormFields Fields) CompileWithFields(Window window, Control root, ImmutableArray<object?> models, bool planning, Control? viewError = null, Overlays? overlays = null, ImmutableDictionary<Control, string>? entrances = null)
    {
        var locals = planning ? Forms.LocalInputs.Of(root) : [];
        var recorder = new PlanRecorder(planning, root.DataContext)
        {
            Locals = locals,
        };
        var sheet = new StyleSheet();
        var writer = new HtmlTreeWriter(recorder, sheet, models);
        if (planning && viewError is not null)
        {
            writer.RecordViewError(viewError);
        }

        writer.Context.Overlays = (overlays ?? Overlays.None).WithoutModals();
        writer.Context.Entrances = entrances ?? Entrances.Settle(window);
        writer.Context.Answering = overlays is not null;
        writer.Context.Modals.Prepare(window, root);
        writer.Write(root, ParentKind.Single);
        Forms.ControlStates.Write(writer.Context, root);
        writer.Context.WriteShape();
        PromptWriters.Write(writer.Context, writer.Context.Overlays, writer.WriteLeftOut);
        if (planning)
        {
            recorder.FailOnUnfollowed();
        }

        var body = recorder.Build();
        var scheme = window.ActualThemeVariant == ThemeVariant.Dark ? "dark" : "light";
        var page = new CompiledPage(
            new RenderPlan([], body, []),
            LineSpacing.Rule(root) + sheet.Build(scheme),
            CssValues.Brush(window.Background) ?? "#fff",
            CssValues.Brush(window.Foreground) ?? "#000",
            Reusable: planning && recorder.Failure is null,
            recorder.Failure)
        {
            Uploads = writer.Fields.Any(field => field is Aioros.Markup.FilePicker),
            FailureAt = recorder.FailureAt,
            Warnings = recorder.Warnings,
        };
        return (page, writer.Fields);
    }
}
