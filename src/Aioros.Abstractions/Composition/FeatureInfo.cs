using System.Collections.Immutable;

namespace Aioros.Composition;

public sealed record FeatureInfo(string Name, ImmutableHashSet<TargetKind> Targets)
{
    public bool Supports(TargetKind target) => Targets.Contains(target);
}
