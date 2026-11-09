using System.Globalization;
using System.Text;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Looks;

internal static class CssValues
{
    public static string? Brush(IBrush? brush) => brush is ISolidColorBrush solid && solid.Color.A > 0
        ? Color(global::Avalonia.Media.Color.FromArgb((byte)(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B))
        : null;

    public static string? Paint(IBrush? brush, global::Avalonia.Size size = default) => Brush(brush) ?? CssGradients.Of(brush, size);

    public static string Matrix(global::Avalonia.Matrix matrix) =>
        $"matrix({Num(matrix.M11)},{Num(matrix.M12)},{Num(matrix.M21)},{Num(matrix.M22)},{Num(matrix.M31)},{Num(matrix.M32)})";

    public static string Color(Color color) => color.A == 255
        ? string.Create(7, color.ToUInt32() & 0xFFFFFF, static (text, rgb) =>
        {
            text[0] = '#';
            rgb.TryFormat(text[1..], out _, "x6", CultureInfo.InvariantCulture);
        })
        : string.Create(CultureInfo.InvariantCulture, $"rgba({color.R},{color.G},{color.B},{Num(color.A / 255.0)})");

    public static string Transform(ITransform transform) => transform is global::Avalonia.Media.Transformation.TransformOperations operations
        ? string.Join(' ', operations.Operations.Select(Function))
        : Matrix(transform.Value);

    public static string Origin(global::Avalonia.RelativePoint origin) => origin.Unit == global::Avalonia.RelativeUnit.Relative
        ? $"{Num(origin.Point.X * 100)}% {Num(origin.Point.Y * 100)}%"
        : $"{Px(origin.Point.X)} {Px(origin.Point.Y)}";

    private static string Function(global::Avalonia.Media.Transformation.TransformOperation operation) => operation.Type switch
    {
        global::Avalonia.Media.Transformation.TransformOperation.OperationType.Translate => $"translate({Px(operation.Data.Translate.X)},{Px(operation.Data.Translate.Y)})",
        global::Avalonia.Media.Transformation.TransformOperation.OperationType.Scale => $"scale({Num(operation.Data.Scale.X)},{Num(operation.Data.Scale.Y)})",
        global::Avalonia.Media.Transformation.TransformOperation.OperationType.Rotate => $"rotate({Num(operation.Data.Rotate.Angle * 180 / Math.PI)}deg)",
        global::Avalonia.Media.Transformation.TransformOperation.OperationType.Skew => $"skew({Num(operation.Data.Skew.X * 180 / Math.PI)}deg,{Num(operation.Data.Skew.Y * 180 / Math.PI)}deg)",
        _ => Matrix(operation.Matrix),
    };

    public static string Px(double value) => Num(value) + "px";

    public static string Num(double value) => (Math.Round(value, 3) + 0.0).ToString(CultureInfo.InvariantCulture);

    public static string Ms(TimeSpan time) => Num(time.TotalMilliseconds) + "ms";

    public static string Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash = (hash ^ ch) * 16777619u;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// An Avalonia easing as a CSS easing function: a spline as its <c>cubic-bezier</c>, the named curves as the
    /// <c>cubic-bezier</c> that draws them (easings.net), and any other easing as a <c>linear()</c> function sampled from it.
    /// </summary>
    public static string Ease(IEasing? easing) => easing switch
    {
        null => "ease",
        LinearEasing => "linear",
        SplineEasing spline => $"cubic-bezier({Num(spline.X1)},{Num(spline.Y1)},{Num(spline.X2)},{Num(spline.Y2)})",
        QuadraticEaseIn => "cubic-bezier(.11,0,.5,0)",
        QuadraticEaseOut => "cubic-bezier(.5,1,.89,1)",
        QuadraticEaseInOut => "cubic-bezier(.45,0,.55,1)",
        CubicEaseIn => "cubic-bezier(.32,0,.67,0)",
        CubicEaseOut => "cubic-bezier(.33,1,.68,1)",
        CubicEaseInOut => "cubic-bezier(.65,0,.35,1)",
        QuarticEaseIn => "cubic-bezier(.5,0,.75,0)",
        QuarticEaseOut => "cubic-bezier(.25,1,.5,1)",
        QuarticEaseInOut => "cubic-bezier(.76,0,.24,1)",
        QuinticEaseIn => "cubic-bezier(.64,0,.78,0)",
        QuinticEaseOut => "cubic-bezier(.22,1,.36,1)",
        QuinticEaseInOut => "cubic-bezier(.83,0,.17,1)",
        SineEaseIn => "cubic-bezier(.12,0,.39,0)",
        SineEaseOut => "cubic-bezier(.61,1,.88,1)",
        SineEaseInOut => "cubic-bezier(.37,0,.63,1)",
        ExponentialEaseIn => "cubic-bezier(.7,0,.84,0)",
        ExponentialEaseOut => "cubic-bezier(.16,1,.3,1)",
        ExponentialEaseInOut => "cubic-bezier(.87,0,.13,1)",
        CircularEaseIn => "cubic-bezier(.55,0,1,.45)",
        CircularEaseOut => "cubic-bezier(0,.55,.45,1)",
        CircularEaseInOut => "cubic-bezier(.85,0,.15,1)",
        BackEaseIn => "cubic-bezier(.36,0,.66,-.56)",
        BackEaseOut => "cubic-bezier(.34,1.56,.64,1)",
        BackEaseInOut => "cubic-bezier(.68,-.6,.32,1.6)",
        _ => $"linear({string.Join(',', Enumerable.Range(0, Samples + 1).Select(index => index == 0 ? "0" : index == Samples ? "1" : Num(easing.Ease((double)index / Samples))))})",
    };

    private const int Samples = 40;

    public static string Encode(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.AsSpan().IndexOfAny("&<>\"") < 0)
        {
            return text ?? string.Empty;
        }

        var builder = new StringBuilder(text.Length + 16);
        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => null,
            } ?? ch.ToString());
        }

        return builder.ToString();
    }

    public static string Join(string? first, string? second) =>
        string.IsNullOrEmpty(first) ? second ?? string.Empty : string.IsNullOrEmpty(second) ? first : first + " " + second;
}
