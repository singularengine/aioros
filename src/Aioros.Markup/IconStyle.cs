using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;

/// <summary>
/// The size the icons inside a control take, as <c>TextElement.FontSize</c> is the size its text takes: an attached
/// property a library sets on a control (Aioros.Cosmo's <c>xs</c> buttons, shadcn/ui's <c>[&amp;_svg]:size-3</c>), which
/// every icon inside inherits unless it sets its own size, on every target.
/// </summary>
public sealed class IconStyle
{
    /// <summary>The size of the icons inside, 16 by default.</summary>
    public static readonly AttachedProperty<double> SizeProperty =
        AvaloniaProperty.RegisterAttached<IconStyle, Control, double>("Size", 16, inherits: true);

    private IconStyle()
    {
    }

    /// <summary>Gets <see cref="SizeProperty"/>.</summary>
    public static double GetSize(Control control) => control.GetValue(SizeProperty);

    /// <summary>Sets <see cref="SizeProperty"/>.</summary>
    public static void SetSize(Control control, double value) => control.SetValue(SizeProperty, value);
}
