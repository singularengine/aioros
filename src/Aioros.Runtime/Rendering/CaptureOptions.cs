using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>How a page is captured for the compatibility bench: its theme, the window size and the interaction state forced on every input element.</summary>
public sealed record CaptureOptions(string Theme, int Width, int Height, string State = CaptureOptions.Normal)
{
    public const string Normal = "normal";

    public static readonly ImmutableArray<string> States = [Normal, "pointerover", "pressed", "focus"];
}
