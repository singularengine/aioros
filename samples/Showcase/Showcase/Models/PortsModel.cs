using System.Collections.Immutable;
using Showcase.Ports;

namespace Showcase.Models;

public sealed record PortCard(string Name, string Kind, string Summary, string Href, string Image);

public sealed record PortsData(ImmutableArray<PortCard> Ports);

public sealed record PortData(
    Port Port,
    string? WebUrl,
    string Precompiled,
    string OriginalImage,
    string AiorosImage);

public sealed class PortsModel(PortsData data)
{
    public ImmutableArray<PortCard> Ports { get; } = data.Ports;
}

public sealed class PortModel(PortData data)
{
    public string Name { get; } = data.Port.Name;

    public string Kind { get; } = data.Port.Kind.ToUpperInvariant();

    public string Summary { get; } = data.Port.Summary;

    public string Source { get; } = data.Port.Source;

    public string SourceUrl { get; } = data.Port.SourceUrl;

    public string? WebUrl { get; } = data.WebUrl;

    public bool HasWebUrl { get; } = data.WebUrl is not null;

    public bool NoWebUrl { get; } = data.WebUrl is null;

    public string RunIt { get; } = $"Run {data.Port.Sample} (dotnet run in its project folder) to open the Aioros web version.";

    public string OriginalImage { get; } = data.OriginalImage;

    public string OriginalCaption { get; } = data.Port.OriginalCapture;

    public string AiorosImage { get; } = data.AiorosImage;

    public string AiorosCaption { get; } = data.Port.AiorosCapture;

    public ImmutableArray<PortChange> Changes { get; } = data.Port.Changes;

    public string Unchanged { get; } = data.Port.Unchanged;

    public string Web { get; } = data.Port.Web;

    public string Native { get; } = data.Port.Native;

    public string Precompiled { get; } = data.Precompiled;
}
