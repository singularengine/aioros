using Aioros.Composition;
using Aioros.Development;
using Aioros.Events;

namespace Aioros.Features.HotReload.Changes;

public sealed class HotReloadFeature : IFeature
{
    public FeatureInfo Info { get; } = new("Aioros.Features.HotReload", [TargetKind.Ssr, TargetKind.Spa, TargetKind.Desktop, TargetKind.Android]);

    public void Register(IFeatureRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
    }

    public static ValueTask AnnounceAsync(IEventBus bus, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.PublishAsync(new SourceChanged(path), cancellationToken);
    }
}
