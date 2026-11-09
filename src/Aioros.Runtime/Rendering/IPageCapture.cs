using System.Collections.Immutable;
using Aioros.Pages;

namespace Aioros.Runtime.Rendering;

public interface IPageCapture
{
    ValueTask<PageCapture> CaptureAsync(PageDefinition page, ImmutableArray<object?> models, CaptureOptions options, CancellationToken cancellationToken);
}
