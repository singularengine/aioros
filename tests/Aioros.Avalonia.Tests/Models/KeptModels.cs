namespace Aioros.Avalonia.Tests.Models;

/// <summary>A form over an item not chosen yet, as Aioros.Cosmo's dashboard viewer over its viewed row.</summary>
public sealed class Viewer
{
    public string Name { get; set; } = "Ada";

    public bool Open { get; set; }

    public Viewer? Viewing { get; set; }
}
