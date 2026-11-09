using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Markup.Xaml.Diagnostics;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The place in the app's XAML of an element, from the positions Avalonia's XAML compiler records (<c>XamlSourceInfo</c>,
/// on in Aioros apps): the element's own, else the nearest parent's, else the nearest control being written that has one.
/// Only a file's positions count: a library's XAML (Avalonia's themes) is compiled into its package without them.
/// </summary>
internal static class SourcePositions
{
    public static SourcePosition? Of(AvaloniaObject? element, IEnumerable<AvaloniaObject> writing) =>
        Up(element).Concat(writing.SelectMany(Up)).Select(Position).FirstOrDefault(position => position is not null);

    private static IEnumerable<AvaloniaObject> Up(AvaloniaObject? element)
    {
        for (var current = element; current is not null; current = (current as StyledElement)?.Parent ?? (current as StyledElement)?.TemplatedParent as AvaloniaObject)
        {
            yield return current;
        }
    }

    private static SourcePosition? Position(AvaloniaObject element) =>
        XamlSourceInfo.GetXamlSourceInfo(element) is { SourceUri: { } uri } info && Source(uri) is { } source
            ? new SourcePosition(source, info.LineNumber, info.LinePosition)
            : null;

    private static string? Source(Uri uri) => uri.IsFile ? uri.LocalPath : null;
}
