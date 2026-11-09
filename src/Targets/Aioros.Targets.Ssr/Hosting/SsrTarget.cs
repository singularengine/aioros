using Aioros.Composition;

namespace Aioros.Targets.Ssr.Hosting;

public sealed class SsrTarget : IFeature
{
    public FeatureInfo Info { get; } = new("Aioros.Targets.Ssr", [TargetKind.Ssr]);

    public void Register(IFeatureRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
    }
}
