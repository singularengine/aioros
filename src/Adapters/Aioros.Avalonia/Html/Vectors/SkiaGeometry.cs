using System.Reflection;
using Avalonia.Media;
using SkiaSharp;

namespace Aioros.Avalonia.Html.Vectors;

internal readonly record struct SvgPath(string Data, bool EvenOdd);

internal static class SkiaGeometry
{
    private static readonly PropertyInfo? PlatformImpl = typeof(Geometry).GetRuntimeProperties().FirstOrDefault(property => property.Name == "PlatformImpl");

    public static SvgPath? Path(Geometry geometry, bool filled = true, bool placed = false)
    {
        var implementation = PlatformImpl?.GetValue(geometry);
        var fill = Skia(implementation, "FillPath");
        var stroke = Skia(implementation, "StrokePath");
        var path = filled && fill is { IsEmpty: false } ? fill : stroke ?? fill;
        if (path is null)
        {
            return null;
        }

        if (!placed || geometry.Transform is not { Value: { IsIdentity: false } matrix })
        {
            return new SvgPath(path.ToSvgPathData(), path.FillType == SKPathFillType.EvenOdd);
        }

        using var moved = new SKPath(path);
        moved.Transform(new SKMatrix((float)matrix.M11, (float)matrix.M21, (float)matrix.M31, (float)matrix.M12, (float)matrix.M22, (float)matrix.M32, 0, 0, 1));
        return new SvgPath(moved.ToSvgPathData(), moved.FillType == SKPathFillType.EvenOdd);
    }

    private static SKPath? Skia(object? implementation, string property) =>
        implementation?.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)?.GetValue(implementation) as SKPath;
}
