using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// A presenter in a control template that shows a property of its templated control (<c>{TemplateBinding Header}</c>,
/// <c>{Binding Icon, RelativeSource={RelativeSource TemplatedParent}}</c>) which the control binds to its item's data, as
/// Ursa's <c>NavMenuItem</c> binds its header and icon through the menu's <c>HeaderBinding</c> and <c>IconBinding</c>:
/// the presenter and what its data template builds read that value, so they are written in its scope, once for each render.
/// A presenter without a data template shows a text, which <see cref="ContentTexts"/> writes.
/// </summary>
internal static class TemplatedContents
{
    private const string TemplatedParentPrefix = "$templatedParent.";

    private const string TemplateBindingPrefix = "{TemplateBinding ";

    /// <summary>The path of the shown value from the current scope, followed, or nothing when the presenter shows something else.</summary>
    public static string? Path(PlanRecorder plan, ContentPresenter presenter)
    {
        if (presenter.TemplatedParent is not StyledElement parent || presenter.Content is Control || presenter.ContentTemplate is null
            || BindingOperations.GetBindingExpressionBase(presenter, ContentPresenter.ContentProperty) is not IDescription { Description: { } description }
            || Shown(description) is not { } name
            || AvaloniaPropertyRegistry.Instance.GetRegistered(parent).FirstOrDefault(property => property.Name == name) is not { } property
            || LocalBindings.Of(parent, property) is null || !plan.InScope(parent))
        {
            return null;
        }

        if (plan.BoundPath(parent, property) is not { } path)
        {
            return null;
        }

        plan.Presented(presenter, ContentPresenter.ContentProperty);
        return path;
    }

    private static string? Shown(string description)
    {
        var name = description.StartsWith(TemplatedParentPrefix, StringComparison.Ordinal) ? description[TemplatedParentPrefix.Length..]
            : description.StartsWith(TemplateBindingPrefix, StringComparison.Ordinal) && description.EndsWith('}') ? description[TemplateBindingPrefix.Length..^1].Trim()
            : null;
        return name is { Length: > 0 } && name.All(char.IsLetterOrDigit) ? name : null;
    }

    public static void Write(PlanRecorder plan, ContentPresenter presenter, string path, Action write)
    {
        if (path.Length == 0)
        {
            write();
            return;
        }

        if (presenter.Content is null)
        {
            plan.Fail($"{presenter.TemplatedParent?.GetType().Name} shows '{path}', which is null in the compiled model, so the plan cannot learn how it shows a value");
            return;
        }

        plan.Begin(presenter.Content);
        write();
        var shown = new SlotSpec("one", path);
        var body = plan.End();
        plan.Begin(plan.Scope);
        using (LocalBindings.Override(presenter, ContentPresenter.ContentProperty, null))
        {
            Settled.Rebuilt(presenter);
            write();
        }

        var none = plan.End();
        Settled.Rebuilt(presenter);
        var set = new SlotSpec("test", path, "set");
        var unset = set with { Argument = "null" };
        plan.Add(new ConditionNode(Slots.Getter(set), [new RepeatNode(Slots.Getter(shown), body) { Source = shown }]) { Source = set });
        plan.Add(new ConditionNode(Slots.Getter(unset), none) { Source = unset });
    }
}
