using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml.Templates;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A <c>ContentControl</c> whose content is bound to data that takes its <c>DataTemplate</c> by type: the presenter is
/// written once per typed template, each learned from the bound value when it is of that type or from a stand-in, and
/// each render picks the case of the value's type.
/// </summary>
internal static class TypedContent
{
    public static bool Applies(Control control) =>
        control is ContentControl owner && owner.GetType() == typeof(ContentControl) && LocalBindings.Of(owner, ContentControl.ContentProperty) is { } binding
        && BindingFormats.StringFormat(binding) is null && owner.Content is not Control && !TemplateSelectors.IsSelector(owner.ContentTemplate) && TypedItems.Templates(owner, owner.ContentTemplate).Any(template => template.DataType is not null);

    public static ContentControl? Owner(Control control) =>
        control is ContentPresenter { TemplatedParent: ContentControl owner } presenter && presenter.Name == "PART_ContentPresenter" && Applies(owner) ? owner : null;

    public static void Write(PlanRecorder plan, ContentControl owner, ContentPresenter presenter, Action write)
    {
        if (plan.BoundPath(owner, ContentControl.ContentProperty) is not { } path)
        {
            return;
        }

        var current = owner.Content;
        var cases = ImmutableArray.CreateBuilder<TypeCase>();
        foreach (var template in TypedItems.Templates(owner, owner.ContentTemplate))
        {
            var type = template.DataType ?? typeof(object);
            if (Sample(current, type) is not { } sample)
            {
                plan.Fail($"a content template for {type.Name} that cannot be learned from a stand-in");
                return;
            }

            using (LocalBindings.Override(owner, ContentControl.ContentProperty, sample))
            {
                TopLevel.GetTopLevel(owner)?.UpdateLayout();
                ThemeLooks.ExpandTemplates(presenter);
                plan.Begin(presenter.DataContext);
                write();
                cases.Add(new TypeCase(type, plan.End()));
            }
        }

        TopLevel.GetTopLevel(owner)?.UpdateLayout();
        var source = new SlotSpec("one", path);
        plan.Add(new RepeatNode(Slots.Getter(source), [new TypeSwitchNode(cases.ToImmutable())]) { Source = source });
    }

    private static object? Sample(object? current, Type type) =>
        type.IsInstanceOfType(current) ? current
        : type.IsAbstract || type.IsInterface ? null
        : type == typeof(object) ? new object()
        : RuntimeHelpers.GetUninitializedObject(type);
}
