using System.Globalization;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Slots;

/// <summary>
/// Reads a move posted to a drop target (<c>index|item</c>, the index the item takes in the target list, or <c>up</c> and
/// <c>down</c> from the move buttons of a page without JavaScript) against the target's item keys, and runs the target's
/// command with <c>(item, target, index)</c>.
/// </summary>
public static class DropMoves
{
    /// <summary>
    /// Runs the move with <paramref name="run"/>, which runs the target's command; a malformed move or an index out of the
    /// list is a bad request, and a step past either end changes nothing.
    /// </summary>
    public static async ValueTask<PostStatus> RunAsync(Func<object?, ValueTask> run, string target, IReadOnlyList<string>? keys, string posted)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(posted);
        var separator = posted.IndexOf('|', StringComparison.Ordinal);
        if (separator <= 0 || separator == posted.Length - 1)
        {
            return PostStatus.BadRequest;
        }

        var item = posted[(separator + 1)..];
        var current = keys is null ? -1 : keys.ToList().IndexOf(item);
        var step = posted[..separator];
        var relative = step is "up" or "down";
        if (Index(step, current) is not { } index)
        {
            return PostStatus.BadRequest;
        }

        var count = keys is null ? int.MaxValue : keys.Count + (current < 0 ? 1 : 0);
        if (index < 0 || index >= count)
        {
            return relative ? PostStatus.Rendered : PostStatus.BadRequest;
        }

        await run((item, target, index));
        return PostStatus.Rendered;
    }

    private static int? Index(string step, int current) => step switch
    {
        "up" or "down" when current < 0 => null,
        "up" => current - 1,
        "down" => current + 1,
        _ => int.TryParse(step, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : null,
    };
}
