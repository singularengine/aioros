using Aioros.Avalonia.Html.Looks;
using Aioros.Runtime.Rendering;
using Aioros.Avalonia.Plans;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Avalonia.Data;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Writers;

internal static class OverlayWriters
{
    public static bool Decorates(Control control) =>
        ToolTip.GetTip(control) is not null || LocalBindings.IsBound(control, ToolTip.TipProperty) || DataValidationErrors.GetHasErrors(control) || control.DataContext is INotifyDataErrorInfo
        || AttachedWriters.Attaches(control);

    /// <summary>Whether the control shows validation errors: it has some, or its model validates a binding of it.</summary>
    public static bool Validates(Control control) =>
        DataValidationErrors.GetHasErrors(control) || (control.DataContext is INotifyDataErrorInfo && Validated(control) is not null);

    /// <summary>
    /// Whether the control shows its validation messages: it validates and its realized template places a
    /// <c>DataValidationErrors</c> (Fluent's text box, numeric box, pickers, combo box and slider; not its button or check
    /// box), where Avalonia shows them; a control without one shows only its <c>:error</c> look.
    /// </summary>
    public static bool ShowsMessages(Control control)
    {
        if (!Validates(control))
        {
            return false;
        }

        ThemeLooks.ExpandTemplates(control);
        return control.GetVisualDescendants().OfType<DataValidationErrors>().Any();
    }

    public static void Write(HtmlContext context, Control control, ParentKind parent, Action<Control, ParentKind> write) =>
        AttachedWriters.Write(context, control, () => WriteDecorated(context, control, parent, write));

    private static void WriteDecorated(HtmlContext context, Control control, ParentKind parent, Action<Control, ParentKind> write)
    {
        var tip = ToolTip.GetTip(control) ?? context.Flags.OtherTip(control);
        var shown = BoundTip(context, control, tip);
        if (tip is not null || shown is not null)
        {
            context.DescribedBy[control] = (null, shown);
        }

        if (shown is { } condition)
        {
            context.Plan.Add(new ConditionNode(condition.Getter, [new LiteralNode(TipWriters.Open(control))]) { Source = condition.Spec });
        }
        else if (tip is not null)
        {
            context.Plan.Literal(TipWriters.Open(control, context.Flags.TipMarker(control)));
        }

        var whole = ShowsMessages(control);
        if (whole)
        {
            var box = BoundStyles.Strip(context.OuterBox(control, parent), BoundStyles.Outer(BoundStyles.Slots(control), outer: true));
            context.Sheet.AddRule("wb", ":where(.wb){display:flex;flex-direction:column;min-width:0}:where(.wb>:first-child){flex:1 1 auto;min-height:0}\n");
            context.Plan.Literal($"<div class=\"{context.Sheet.Class("wb", box)}\"");
            context.OuterStyle(control);
            context.Plan.Literal(">");
            context.Wrapped.Add(control);
        }

        write(control, whole ? ParentKind.Column : parent);
        context.Wrapped.Remove(control);
        var described = context.Describes(control);
        if (described)
        {
            context.Sheet.AddRule("vw", ".vw{display:contents}\n");
            context.Plan.Literal("<div id=\"e");
            context.Plan.Add(SameIdNode.Instance);
            context.Plan.Literal("\" class=\"vw\">");
        }

        if (whole && context.Plan.Planning && control.DataContext is INotifyDataErrorInfo && Validated(control) is { } validated)
        {
            PlanErrors(context, control, validated, described);
        }
        else if (whole)
        {
            WriteErrors(context, control);
        }

        context.Plan.Literal(described ? "</div>" : string.Empty);
        context.Plan.Literal(whole ? "</div>" : string.Empty);

        if (shown is { } bound)
        {
            context.Plan.Begin(context.Plan.Scope);
            TipWriters.Write(context, control, tip, context.TipId(control));
            context.Plan.Literal("</span>");
            context.Plan.Add(new ConditionNode(bound.Getter, context.Plan.End()) { Source = bound.Spec });
        }
        else if (tip is not null)
        {
            TipWriters.Write(context, control, tip, context.TipId(control));
            context.Plan.Literal("</span>");
        }
    }

    /// <summary>
    /// Whether a tip bound to a text of the model is set, which shows the tip (Avalonia shows none without content), or
    /// nothing when the tip is not bound to a path.
    /// </summary>
    private static Slot? BoundTip(HtmlContext context, Control control, object? tip)
    {
        if (!context.Plan.Planning || tip is ToolTip or Control || !LocalBindings.IsBound(control, ToolTip.TipProperty))
        {
            return null;
        }

        if (BoundConverters.Of(context.Plan, control, ToolTip.TipProperty) is { Spec: { } converted })
        {
            return Slots.Of(converted with { Values = converted.Values.SetItem(3, "set") });
        }

        return context.Plan.BoundPath(control, ToolTip.TipProperty) is { } path ? Slots.Of(new SlotSpec("test", path, "set")) : null;
    }

    public static void WriteValidation(HtmlContext context, DataValidationErrors errors, ParentKind parent)
    {
        context.Content(errors.Content);
        WriteErrors(context, errors.Owner ?? errors);
    }

    /// <summary>
    /// The property whose binding Avalonia validates (a local binding of a property whose metadata enables data
    /// validation, other than a command), or nothing when the control binds none, as a template part does.
    /// </summary>
    private static AvaloniaProperty? Validated(Control control) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(control)
            .FirstOrDefault(property => property.GetMetadata(control.GetType()).EnableDataValidation == true && property.PropertyType != typeof(System.Windows.Input.ICommand)
                && LocalBindings.Of(control, property) is not null);

    /// <summary>The path of the model member whose errors the control shows, or nothing when it binds none the plan follows.</summary>
    public static string? ValidatedPath(PlanRecorder plan, Control control) =>
        Validated(control) is { } validated ? plan.PathOf(control, validated) : null;

    private static void PlanErrors(HtmlContext context, Control control, AvaloniaProperty validated, bool described)
    {
        var path = context.Plan.PathOf(control, validated);
        if (path is null && ReadsElement(control, validated))
        {
            WriteErrors(context, control);
            return;
        }

        if (path is null)
        {
            context.Plan.Fail($"{control.GetType().Name} is bound to a model with data validation through a path that cannot be planned");
            return;
        }

        var name = ErrorClass(context, control);
        if (!described)
        {
            var color = ThemeResources.Css(control, "SystemControlErrorTextForegroundBrush", "#c42b1c");
            context.Sheet.AddRule(name + "-field", $":has(+.{name}){{border-color:{color}!important}}\n");
        }

        var errors = Slots.ErrorsOf(path);
        var text = Slots.FormatOf("error-text");
        context.Plan.Add(new RepeatNode(errors.Getter, [
            new LiteralNode($"<div role=\"alert\" class=\"{name}\">"),
            new ValueNode(Slots.Self.Getter, text.Formatter) { Source = Slots.Self.Spec, FormatSpec = text.Spec },
            new LiteralNode("</div>"),
        ]) { Source = errors.Spec });
    }

    /// <summary>
    /// Whether the validated binding reads a property of an element (its templated control's, as the parts of Ursa's
    /// pickers bind theirs, or a named element's) and not the model: Avalonia validates the element, which reports no errors.
    /// </summary>
    private static bool ReadsElement(Control control, AvaloniaProperty validated) =>
        LocalBindings.Of(control, validated) is IDescription { Description: { } path } && !path.Contains("DataContext", StringComparison.Ordinal)
        && (path.StartsWith("$templatedParent.", StringComparison.Ordinal) || ElementBindings.ElementOf(control, path) is not null);

    private static string ErrorClass(HtmlContext context, Control control)
    {
        var css = ErrorLook(control) ?? $"color:{ThemeResources.Css(control, "SystemControlErrorTextForegroundBrush", "#c42b1c")};font-size:12px;margin-top:4px;";
        var name = "ve" + CssValues.Hash(css);
        context.Sheet.AddRule(name, $".{name}{{{css}overflow-wrap:anywhere}}\n");
        return name;
    }

    /// <summary>
    /// The look of a message as the control's <c>DataValidationErrors</c> theme writes it: its error template realized for
    /// a message (the control's own, or a probe set with an animation's priority and taken back), its text's color, font
    /// and line height, and the margins between it and the field, both inside the element that holds the control's whole
    /// box; nothing when the control's template shows no messages.
    /// </summary>
    private static string? ErrorLook(Control control)
    {
        ThemeLooks.ExpandTemplates(control);
        if ((control as DataValidationErrors ?? control.GetVisualDescendants().OfType<DataValidationErrors>().FirstOrDefault()) is not { } errors)
        {
            return null;
        }

        using var probe = DataValidationErrors.GetHasErrors(control) ? null : control.SetValue(DataValidationErrors.ErrorsProperty, Probe, BindingPriority.Animation);
        ThemeLooks.ExpandTemplates(errors);
        var shown = DataValidationErrors.GetErrors(control)?.Select(error => error is Exception exception ? exception.Message : error?.ToString()).ToHashSet(StringComparer.Ordinal) ?? [];
        if (errors.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Text is { } message && shown.Contains(message)) is not { } text)
        {
            return null;
        }

        var margin = text.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, errors)).OfType<Control>().Prepend(text)
            .Aggregate(default(Thickness), (sum, part) => sum + part.Margin);
        var css = new StringBuilder();
        BoxStyles.Font(css, text.FontSize, text.FontWeight, text.Foreground);
        css.Append(text.LineHeight > 0 ? $"line-height:{CssValues.Px(text.LineHeight)};" : string.Empty);
        css.Append(margin == default ? string.Empty : $"margin:{BoxStyles.Edges(margin)};");
        return css.ToString();
    }

    private static readonly object[] Probe = ["\u2060"];

    private static void WriteErrors(HtmlContext context, Control control)
    {
        if (DataValidationErrors.GetErrors(control) is not { } errors || !errors.Any())
        {
            return;
        }

        if (context.Plan.Planning && !SetInXaml(control))
        {
            context.Plan.Fail($"{control.GetType().Name} shows data validation errors");
        }

        var name = ErrorClass(context, control);
        foreach (var error in errors)
        {
            var message = error is Exception exception ? exception.Message : Convert.ToString(error, CultureInfo.InvariantCulture);
            context.Plan.Literal($"<div role=\"alert\" class=\"{name}\">{CssValues.Encode(message)}</div>");
        }
    }

    /// <summary>
    /// Whether the control's errors are a constant of the page: set in the XAML (<c>DataValidationErrors.Error</c>),
    /// bound to nothing, on a control with no binding Avalonia validates.
    /// </summary>
    private static bool SetInXaml(Control control) =>
        Validated(control) is null && !LocalBindings.HasBinding(control, DataValidationErrors.ErrorsProperty);
}
