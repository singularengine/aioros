using System.Collections.Immutable;

namespace Aioros.Runtime.Composition;

public sealed record CompositionResult(bool IsSuccess, AiorosComposition Composition, ImmutableArray<string> Errors)
{
    public static CompositionResult Succeeded(AiorosComposition composition) => new(IsSuccess: true, composition, []);

    public static CompositionResult Failed(IEnumerable<string> errors) =>
        new(IsSuccess: false, AiorosComposition.Empty(default), [.. errors]);
}
