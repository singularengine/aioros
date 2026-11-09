using OxyPlot;

namespace Aioros.Avalonia.Charts;

internal static class ChartText
{
    private const double Ascent = 0.96875;

    private const double Descent = 0.2421875;

    public static OxySize Measure(string? text, double size, double weight) =>
        string.IsNullOrEmpty(text) ? OxySize.Empty : new OxySize(InterMetrics.Width(text, size, weight >= 600), InterMetrics.Height(size));

    public static double Baseline(double y, double size, VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => y + (Ascent * size),
        VerticalAlignment.Middle => y + (Ascent * size) - (InterMetrics.Height(size) / 2),
        _ => y - (Descent * size),
    };

    public static double Left(double x, double width, HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => x - (width / 2),
        HorizontalAlignment.Right => x - width,
        _ => x,
    };
}
