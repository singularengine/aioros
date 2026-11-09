using System.Globalization;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// What a writer writes for a popover's id while a plan is recorded: a mark that <see cref="PlanRecorder.Literal"/> turns
/// into a <see cref="Runtime.Rendering.PopoverNode"/>, which reserves a new id where the mark first appears and repeats it
/// after, so each render numbers the popovers it writes as Avalonia's render numbers them.
/// </summary>
internal static class PopoverMarks
{
    public const char Open = '\u0002';

    public const char Close = '\u0003';

    public static string Of(int number) => Open + number.ToString(CultureInfo.InvariantCulture) + Close;
}
