using System.Buffers;
using System.Collections.Immutable;
using Aioros.Avalonia.Html;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// Compiles a page twice, as Avalonia renders it and as a plan, and keeps the plan only when it writes the same bytes
/// the same style sheet and the same keyed styles for the models it was compiled with; otherwise Avalonia's render with the plan's failure.
/// Both compiles write the entrances the page's transitions were in the middle of when it was shown (an overlay open from
/// the start, entering), settled once before the first, so the second does not find them already landed.
/// </summary>
internal static class VerifiedPlans
{
    public static CompiledPage Compile(Window window, Control root, ImmutableArray<object?> models, Control? viewError)
    {
        var entrances = Html.Looks.Entrances.Settle(window);
        var literal = HtmlCompiler.Compile(window, root, models, planning: false, entrances: entrances);
        CompiledPage planned;
        try
        {
            planned = HtmlCompiler.Compile(window, root, models, planning: true, viewError, entrances: entrances);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return literal with { Failure = error.Message };
        }

        if (!planned.Reusable)
        {
            return literal with { Failure = planned.Failure, FailureAt = planned.FailureAt };
        }

        return Mismatch(literal, planned, models) is { } reason ? literal with { Failure = "the compiled plan does not reproduce the Avalonia render: " + reason } : planned;
    }

    private static string? Mismatch(CompiledPage literal, CompiledPage planned, ImmutableArray<object?> models)
    {
        var expected = new ArrayBufferWriter<byte>();
        var actual = new ArrayBufferWriter<byte>();
        var (written, contributed) = (new PageStyles(), new PageStyles());
        using (StateSeals.Use(null))
        {
            literal.Plan.RenderBody(expected, models, styles: written);
            planned.Plan.RenderBody(actual, models, styles: contributed);
        }

        var at = expected.WrittenSpan.CommonPrefixLength(actual.WrittenSpan);
        if (at < expected.WrittenCount || at < actual.WrittenCount)
        {
            return $"the body differs at byte {at}: Avalonia wrote \"{Excerpt(expected.WrittenSpan, at)}\" and the plan \"{Excerpt(actual.WrittenSpan, at)}\"";
        }

        if (!written.All.SequenceEqual(contributed.All))
        {
            return $"the page's keyed styles differ: Avalonia contributed {string.Join(", ", written.All.Select(static style => style.Key))} and the plan {string.Join(", ", contributed.All.Select(static style => style.Key))}";
        }

        var rules = planned.Css.Split('\n').ToHashSet(StringComparer.Ordinal);
        return literal.Css.Split('\n').FirstOrDefault(rule => !rules.Contains(rule)) is { } missing ? $"the plan's style sheet lacks \"{missing}\"" : null;
    }

    private static string Excerpt(ReadOnlySpan<byte> html, int at)
    {
        var start = Math.Max(0, at - 40);
        return System.Text.Encoding.UTF8.GetString(html[start..Math.Min(html.Length, at + 60)]);
    }
}
