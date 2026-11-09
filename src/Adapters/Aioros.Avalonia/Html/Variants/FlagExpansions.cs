using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>The disclosure state a flagged panel's button writes.</summary>
internal static class FlagExpansions
{
    /// <summary>Writes whether a button's panel is open, as the WAI-ARIA disclosure pattern's <c>aria-expanded</c>, read from the model when the flag is bound.</summary>
    public static void WriteExpanded(HtmlContext context, Panel owner, AvaloniaProperty property)
    {
        var plan = context.Plan;
        if (plan.Planning && plan.BoundPath(owner, property) is { } path)
        {
            var fallback = (property.GetMetadata(owner.GetType()) as IStyledPropertyMetadata)?.DefaultValue is true;
            plan.Literal(" aria-expanded=\"");
            foreach (var (negate, text) in new[] { (false, "true"), (true, "false") })
            {
                var slot = Flags.Of(path, negate, fallback);
                plan.Add(new ConditionNode(slot.Getter, [new LiteralNode(text)]) { Source = slot.Spec });
            }

            plan.Literal("\"");
            return;
        }

        plan.Literal($" aria-expanded=\"{(owner.GetValue(property) is true ? "true" : "false")}\"");
    }
}
