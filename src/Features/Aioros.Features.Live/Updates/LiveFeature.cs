using Aioros.Composition;

namespace Aioros.Features.Live.Updates;

public sealed class LiveFeature : IFeature
{
    public FeatureInfo Info { get; } = new("Aioros.Features.Live", [TargetKind.Ssr, TargetKind.Spa, TargetKind.Desktop, TargetKind.Android]);

    public void Register(IFeatureRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
    }
}
