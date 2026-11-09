using Aioros.Pages;
using Microsoft.Extensions.Configuration;
using Showcase.Models;
using Showcase.Ports;

namespace Showcase.Pages.Ports;

public sealed class PortLoader(IConfiguration configuration)
{
    public LoaderResult<PortData> Load(string app)
    {
        if (PortCatalog.Find(app) is not { } port)
        {
            return LoaderResult.NotFound();
        }

        return LoaderResult.With(
            new PortData(
                port,
                configuration[$"Ports:{port.Slug}"] is { Length: > 0 } url ? url : null,
                port.Precompiled,
                PortCatalog.Image(port.Slug, "original"),
                PortCatalog.Image(port.Slug, "aioros")),
            port.Name + " on Aioros · Aioros Showcase",
            new PageMetadata($"{port.Name} ported to Aioros: the original beside the port, what changed, and how it runs."));
    }
}
