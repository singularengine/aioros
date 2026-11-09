using Aioros.Markup;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Two sliders that are one range (<see cref="Web.RangeProperty"/>, Aioros.Cosmo's two-thumb slider): their panel is
/// marked <c>data-aioros-range</c> and holds, after them, the hidden <c>_range</c> field naming their fields in order, so a
/// post carrying them crossed binds them in order (<see cref="PlanForms.Ranges"/>); each range input leaves the pointer to
/// its thumb (both thumbs draggable without the script) and the range module of the framework's script does the rest
/// (RangeScript).
/// </summary>
internal static class RangeGroups
{
    private const string LowerKey = "range-lower";

    private const string UpperKey = "range-upper";

    private const string Css = "[data-aioros-range]{touch-action:none}[data-aioros-range]>input[type=range]{pointer-events:none}[data-aioros-range]>input[type=range]::-webkit-slider-thumb{pointer-events:auto}[data-aioros-range]>input[type=range]::-moz-range-thumb{pointer-events:auto}\n";

    /// <summary>Whether a control is a panel marked as a range that holds exactly two sliders.</summary>
    public static bool Marks(Control control) =>
        control is Panel panel && Web.GetRange(panel) && panel.Children.Count == 2 && panel.Children.All(child => child is Slider);

    /// <summary>The key a slider's field id is remembered under when it is one of a range's two sliders, or nothing.</summary>
    public static string? Key(Slider slider) =>
        slider.GetVisualParent() is Panel panel && Marks(panel) ? ReferenceEquals(panel.Children[0], slider) ? LowerKey : UpperKey : null;

    /// <summary>The range's mark, written at its panel's element.</summary>
    public static void Attributes(HtmlContext context, Control control)
    {
        if (Marks(control))
        {
            context.Sheet.AddRule("range", Css);
            context.Plan.Literal(" data-aioros-range");
        }
    }

    /// <summary>The hidden field naming the range's two fields, written after its sliders.</summary>
    public static void Close(HtmlContext context, Control control)
    {
        if (!Marks(control))
        {
            return;
        }

        context.Plan.Literal($"<input type=\"hidden\" name=\"{PlanForms.RangeField}\" value=\"f");
        context.Plan.Add(new SameIdNode(LowerKey));
        context.Plan.Literal(" f");
        context.Plan.Add(new SameIdNode(UpperKey));
        context.Plan.Literal("\">");
    }
}
