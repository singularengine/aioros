using Aioros.Avalonia.Trees;
using Aioros.Composition;

namespace Aioros.Targets.Desktop.Hosting;

public sealed class DesktopTarget : IFeature
{
    public FeatureInfo Info { get; } = new($"Aioros.Targets.Desktop ({AvaloniaUiTree.EngineName})", [TargetKind.Desktop]);

    public void Register(IFeatureRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
    }
}
