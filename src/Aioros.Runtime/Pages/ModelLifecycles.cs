using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Tells the page's <see cref="IModelLifecycle"/>, when its scope holds one, of the models a load created: every model
/// of the slots, the items of repeated views included, except those the page already had before (kept or receiving
/// fresh data).
/// </summary>
internal static class ModelLifecycles
{
    public static ValueTask ReportAsync(IServiceProvider services, IReadOnlyList<object?> models, ImmutableArray<object?> before, PageRequest request, CancellationToken cancellationToken) =>
        services.GetService(typeof(IModelLifecycle)) is IModelLifecycle lifecycle
            ? ReportAsync(lifecycle, models, before, request, cancellationToken)
            : ValueTask.CompletedTask;

    private static async ValueTask ReportAsync(IModelLifecycle lifecycle,IReadOnlyList<object?> models, ImmutableArray<object?> before, PageRequest request, CancellationToken cancellationToken)
    {
        var known = before.IsDefaultOrEmpty ? null : Models(before).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var model in Models(models))
        {
            if (known?.Contains(model) != true)
            {
                await lifecycle.CreatedAsync(model, request, cancellationToken);
            }
        }
    }

    private static IEnumerable<object> Models(IEnumerable<object?> slots) =>
        slots.SelectMany(static slot => slot is ViewItems items ? items.Items.Select(static item => item.Model) : [slot])
            .Where(static model => model is not (null or ViewState))
            .Select(static model => model!);
}
