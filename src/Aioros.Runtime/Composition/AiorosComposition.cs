using System.Collections.Immutable;
using Aioros.Composition;

namespace Aioros.Runtime.Composition;

public sealed record AiorosComposition(
    TargetKind Target,
    ImmutableArray<IFeature> Features,
    ImmutableArray<ServiceRegistration> Services)
{
    public static AiorosComposition Empty(TargetKind target) => new(target, [], []);
}
