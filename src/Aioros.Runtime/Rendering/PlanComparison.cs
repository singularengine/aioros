using System.Buffers;
using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>The outcome of rendering one page and theme from the same plan read in two ways.</summary>
public sealed record PlanCheck(string Route, string Theme, int? Difference)
{
    /// <summary>The miss the saved plan met for the page's models loaded anew (PRODUCTION-31), or nothing when it renders them.</summary>
    public string? Miss { get; init; }
}

public static class PlanComparison
{
    /// <summary>The offset of the first byte where the two plans render these models differently, or nothing when they render the same.</summary>
    public static int? FirstDifference(RenderPlan left, RenderPlan right, ImmutableArray<object?> models, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var first = new ArrayBufferWriter<byte>();
        var second = new ArrayBufferWriter<byte>();
        left.Render(first, models, path: path);
        right.Render(second, models, path: path);
        var same = first.WrittenSpan.CommonPrefixLength(second.WrittenSpan);
        return same == first.WrittenCount && same == second.WrittenCount ? null : same;
    }

    /// <summary>The miss a plan meets rendering these models (a <see cref="PlanMissException"/>), or nothing when it renders them.</summary>
    public static string? Miss(RenderPlan plan, ImmutableArray<object?> models, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            plan.Render(new ArrayBufferWriter<byte>(), models, path: path);
            return null;
        }
        catch (PlanMissException miss)
        {
            return miss.Message;
        }
    }
}
