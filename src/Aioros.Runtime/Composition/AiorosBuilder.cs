using System.Collections.Immutable;
using Aioros.Composition;

namespace Aioros.Runtime.Composition;

public sealed class AiorosBuilder(TargetKind target)
{
    private ImmutableList<IFeature> _features = [];

    public AiorosBuilder Add(IFeature feature)
    {
        _features = _features.Add(feature);
        return this;
    }

    public CompositionResult Build()
    {
        var unsupported = _features.Where(f => !f.Info.Supports(target)).Select(f => f.Info.Name).ToImmutableArray();
        if (!unsupported.IsEmpty)
        {
            return CompositionResult.Failed(unsupported.Select(name => $"{name} does not support {target}"));
        }

        var registrar = new FeatureRegistrar(target);
        foreach (var feature in _features)
        {
            feature.Register(registrar);
        }

        return CompositionResult.Succeeded(new AiorosComposition(target, _features.ToImmutableArray(), registrar.Registrations));
    }
}
