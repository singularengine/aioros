using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A <c>ContentControl</c> whose bound content takes its template from an app's template selector, which Avalonia's
/// presenter asks before any typed template. Content with a few values (an enum, a <c>bool</c>) is written once per
/// value, as the selector builds it, whatever code it runs; other content is written once per template the selector
/// holds, and each render asks the rebuilt selector which one the content takes.
/// </summary>
internal static class ChosenContent
{
    private const int MostValues = 32;

    public static bool Applies(Control control) =>
        control is ContentControl owner && owner.GetType() == typeof(ContentControl) && LocalBindings.Of(owner, ContentControl.ContentProperty) is { } binding
        && BindingFormats.StringFormat(binding) is null && owner.Content is not Control && TemplateSelectors.IsSelector(owner.ContentTemplate);

    public static ContentControl? Owner(Control control) =>
        control is ContentPresenter { TemplatedParent: ContentControl owner } presenter && presenter.Name == "PART_ContentPresenter" && Applies(owner) ? owner : null;

    public static void Write(PlanRecorder plan, ContentControl owner, ContentPresenter presenter, Action write)
    {
        if (plan.BoundPath(owner, ContentControl.ContentProperty) is not { } path)
        {
            return;
        }

        var selector = owner.ContentTemplate!;
        var cases = Values(owner.Content) is { } values ? ByValue(plan, owner, presenter, write, values) : ByTemplate(plan, owner, presenter, write, selector);
        TopLevel.GetTopLevel(owner)?.UpdateLayout();
        if (cases is null)
        {
            return;
        }

        var source = new SlotSpec("one", path);
        plan.Add(new RepeatNode(Slots.Getter(source), cases.Value) { Source = source });
    }

    /// <summary>The values content of this kind can take, with none: an enum's, or a <c>bool</c>'s.</summary>
    private static List<object?>? Values(object? content) => content switch
    {
        bool => [false, true, null],
        Enum value when !value.GetType().IsDefined(typeof(FlagsAttribute), inherit: false) && Enum.GetValues(value.GetType()) is { Length: <= MostValues } all =>
            [.. all.Cast<object>().Distinct(), null],
        _ => null,
    };

    private static ImmutableArray<IPlanNode>? ByValue(PlanRecorder plan, ContentControl owner, ContentPresenter presenter, Action write, List<object?> values)
    {
        var cases = ImmutableArray.CreateBuilder<IPlanNode>();
        foreach (var value in values)
        {
            var spec = new SlotSpec("value", null, value?.GetType().FullName) { Values = value is null ? [] : [value.ToString()!] };
            cases.Add(new ConditionNode(Slots.Getter(spec), Learn(plan, owner, presenter, write, value, null)) { Source = spec });
        }

        return cases.ToImmutable();
    }

    private static ImmutableArray<IPlanNode>? ByTemplate(PlanRecorder plan, ContentControl owner, ContentPresenter presenter, Action write, IDataTemplate selector)
    {
        var (held, failure) = TemplateSelectors.Held(selector);
        if (failure is not null || held.IsEmpty)
        {
            plan.Fail(failure ?? $"the template selector {selector.GetType().Name} builds its controls in code for {owner.Content?.GetType().Name ?? "no"} content, which a plan learns only for an enum or a bool");
            return null;
        }

        var spec = TemplateSelectors.Spec(selector, held);
        var choose = Slots.Resolver.Chooser(spec);
        var current = owner.Content;
        var bodies = ImmutableArray.CreateBuilder<ImmutableArray<IPlanNode>>();
        for (var index = 0; index < held.Length; index++)
        {
            var sample = current is not null && choose(current) == index ? current : TemplateSelectors.StandIn(held[index].Template);
            if (sample is null)
            {
                plan.Fail(TemplateSelectors.Missing(selector, held[index].Entry));
                return null;
            }

            bodies.Add(Learn(plan, owner, presenter, write, sample, held[index].Template));
        }

        var none = new SlotSpec("value");
        return [new ConditionNode(Slots.Getter(none), Learn(plan, owner, presenter, write, null, null)) { Source = none }, new SelectorNode(choose, bodies.ToImmutable()) { Selector = spec }];
    }

    private static ImmutableArray<IPlanNode> Learn(PlanRecorder plan, ContentControl owner, ContentPresenter presenter, Action write, object? content, IDataTemplate? template)
    {
        using (template is null ? null : LocalBindings.Override(owner, ContentControl.ContentTemplateProperty, template))
        using (LocalBindings.Override(owner, ContentControl.ContentProperty, content))
        {
            TopLevel.GetTopLevel(owner)?.UpdateLayout();
            ThemeLooks.ExpandTemplates(presenter);
            plan.Begin(presenter.DataContext);
            write();
            return plan.End();
        }
    }
}
