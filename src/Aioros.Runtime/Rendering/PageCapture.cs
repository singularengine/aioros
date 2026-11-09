using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>A captured frame and the texts laid out in it.</summary>
public sealed record PageCapture(byte[] Png, ImmutableArray<LaidOutText> Texts);
