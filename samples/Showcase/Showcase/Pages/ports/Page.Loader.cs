using Aioros.Pages;
using Showcase.Models;
using Showcase.Ports;

namespace Showcase.Pages.Ports;

public sealed class PortsLoader
{
    public static LoaderResult<PortsData> Load() =>
        LoaderResult.With(
            new PortsData([.. PortCatalog.All.Select(port => new PortCard(port.Name, port.Kind.ToUpperInvariant(), port.Summary, "/ports/" + port.Slug, PortCatalog.Image(port.Slug, "original")))]),
            "Ported apps · Aioros Showcase",
            new PageMetadata("Open-source Avalonia apps ported to Aioros, beside their originals, with what changed."));
}
