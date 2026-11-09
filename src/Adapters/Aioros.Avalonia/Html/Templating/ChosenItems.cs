using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A list whose item template is an app's template selector: each template the selector holds is learned from the
/// list's first item the selector gives it, or from a stand-in of the template's data type, and each item asks the
/// rebuilt selector which one it takes, as MAUI's and WinUI's <c>DataTemplateSelector</c> map the template returned.
/// </summary>
internal static class ChosenItems
{
    public static void Write(PlanRecorder plan, ItemsControl items, Panel panel, string path, Func<Control, ImmutableArray<IPlanNode>> write)
    {
        var selector = items.ItemTemplate!;
        var (held, failure) = TemplateSelectors.Held(selector);
        if (failure is not null || held.IsEmpty)
        {
            plan.Fail(failure ?? $"the template selector {selector.GetType().Name} builds its controls in code for the items of a list, which a plan does not learn");
            return;
        }

        var spec = TemplateSelectors.Spec(selector, held);
        var choose = Slots.Resolver.Chooser(spec);
        var list = items.ItemsSource?.Cast<object?>().ToList() ?? [];
        var bodies = ImmutableArray.CreateBuilder<ImmutableArray<IPlanNode>>();
        for (var index = 0; index < held.Length; index++)
        {
            var taken = list.FindIndex(item => choose(item) == index);
            var sample = taken >= 0 ? list[taken] : TemplateSelectors.StandIn(held[index].Template);
            if (sample is null)
            {
                plan.Fail(TemplateSelectors.Missing(selector, held[index].Entry));
                return;
            }

            using (LocalBindings.Override(items, ItemsControl.ItemTemplateProperty, held[index].Template))
            using (var standIn = StandIns.Fill(items, sample))
            {
                if (standIn is null || panel.Children.FirstOrDefault() is not Control learned)
                {
                    plan.Fail(TemplateSelectors.Missing(selector, held[index].Entry));
                    return;
                }

                bodies.Add(write(learned));
            }
        }

        plan.Add(ItemIdentity.Keyed(plan, new RepeatNode(PropertyPaths.Getter(path), [new SelectorNode(choose, bodies.ToImmutable()) { Selector = spec }]) { Source = SlotSpec.Bound(path) }, ItemIdentity.Path(items, null)));
    }
}
