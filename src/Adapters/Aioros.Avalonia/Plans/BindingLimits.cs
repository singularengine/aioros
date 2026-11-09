using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Plans;

/// <summary>Bindings a plan cannot reproduce yet; a page that has one is not planned rather than planned wrong.</summary>
internal static class BindingLimits
{
    public static string? Unreproducible(Control control)
    {
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(control).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(control.GetType())))
        {
            if (BindingOperations.GetBindingExpressionBase(control, property) is not { } expression || BindingSources.IsConstant(expression, control))
            {
                continue;
            }

            if (expression.GetType().Name.Contains("MultiBinding", StringComparison.Ordinal) && !Converts(control, property))
            {
                return $"{control.GetType().Name}.{property.Name} is bound with a MultiBinding";
            }

            if (BindingFormats.Converter(expression) is { } converter && (!BoundConverters.Plain(property.PropertyType) || BoundConverters.Spec(expression, string.Empty, property.PropertyType) is null) && !Converts(control, property) && !Html.Variants.LearnedValues.Learns(control, property))
            {
                return $"{control.GetType().Name}.{property.Name} is bound through a converter ({converter.GetType().Name})";
            }
        }

        return null;
    }

    private static bool Converts(Control control, AvaloniaProperty property) =>
        property == Visual.IsVisibleProperty || property == TextBlock.TextProperty || property == ToolTip.TipProperty || property == global::Avalonia.Automation.AutomationProperties.NameProperty || property.Name.StartsWith("__AvaloniaReserved::Classes::", StringComparison.Ordinal)
        || (property == Border.BackgroundProperty && control is Border) || (property == Panel.BackgroundProperty && control is Panel)
        || (property == global::Avalonia.Controls.Documents.TextElement.ForegroundProperty && control is TextBlock)
        || (property == global::Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty && control is RadioButton);
}
