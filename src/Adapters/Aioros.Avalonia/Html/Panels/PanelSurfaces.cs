using System.Globalization;
using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Panels;

internal static class PanelSurfaces
{
    public static void Surface(Control control, StringBuilder style)
    {
        if (control is Border border)
        {
            var radius = BoundStyles.Radius(border) ? default : border.CornerRadius;
            var padding = BoundStyles.Padding(border) ? default : border.Padding;
            BoxStyles.Fill(style, BoundStyles.Bound(border, Border.BackgroundProperty) ? null : border.Background, border.BorderBrush, border.BorderThickness, radius, padding, resetBorder: false, size: BoxStyles.Sized(border));
            style.Append(border.BoxShadow.Count > 0 && !BoundStyles.Shadow(border) ? "box-shadow:" + BoxStyles.Shadow(border.BoxShadow) + ";" : string.Empty);
        }
        else if (control is ContentPresenter presenter && Framed(presenter))
        {
            Presented(presenter, style);
        }
        else if (control is Panel panel && !BoundStyles.Bound(panel, Panel.BackgroundProperty) && CssValues.Paint(panel.Background, BoxStyles.Sized(panel)) is { } background)
        {
            style.Append("background:").Append(background).Append(';');
        }
    }
    public static void LayoutUniform(UniformGrid uniform, StringBuilder style)
    {
        style.Append("display:grid;min-width:0;");
        style.Append(uniform.Rows == 1 && uniform.Columns == 0
            ? "grid-auto-flow:column;grid-auto-columns:minmax(0,1fr);"
            : $"grid-template-columns:repeat({Math.Max(1, uniform.Columns)},minmax(0,1fr));");
        style.Append(CultureInfo.InvariantCulture, $"column-gap:{CssValues.Px(uniform.ColumnSpacing)};row-gap:{CssValues.Px(uniform.RowSpacing)};");
    }
    public static bool IsWrapper(Control control)
    {
        if (control is Panel or ScrollViewer || control is not (ContentPresenter or ItemsPresenter or TemplatedControl or Decorator))
        {
            return false;
        }

        if (control is ContentPresenter presenter)
        {
            return !Framed(presenter);
        }

        return control is not Border border || (CssValues.Paint(border.Background) is null && border.BorderThickness == default
            && border.Padding == default && border.CornerRadius == default && border.BoxShadow.Count == 0
            && !BoundStyles.Bound(border, Border.BackgroundProperty));
    }

    private static void Presented(ContentPresenter presenter, StringBuilder style)
    {
        BoxStyles.Fill(style, BoundStyles.Bound(presenter, ContentPresenter.BackgroundProperty) ? null : presenter.Background, presenter.BorderBrush, presenter.BorderThickness, presenter.CornerRadius, presenter.Padding, resetBorder: false, size: BoxStyles.Sized(presenter));
        style.Append(presenter.BoxShadow.Count > 0 ? "box-shadow:" + BoxStyles.Shadow(presenter.BoxShadow) + ";" : string.Empty);
    }

    /// <summary>
    /// Whether a template's content presenter draws a box of its own, as a <c>Border</c> does: a fill, a border, a
    /// shadow or padding (a library control's <c>Padding</c> its template gives the presenter), which the web writes.
    /// </summary>
    private static bool Framed(ContentPresenter presenter) =>
        CssValues.Paint(presenter.Background) is not null || presenter.BorderThickness != default || presenter.Padding != default || presenter.BoxShadow.Count > 0;
}
