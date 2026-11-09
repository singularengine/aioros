namespace Aioros.Runtime.Rendering;

/// <summary>A text as laid out in a captured frame: the bounds of its glyph lines in window pixels.</summary>
public readonly record struct LaidOutText(string Text, double X, double Y, double Width, double Height);
