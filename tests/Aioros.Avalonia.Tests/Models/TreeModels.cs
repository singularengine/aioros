using System.Collections.Immutable;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Folder(string name, params Folder[] items)
{
    public string Name { get; } = name;

    public ImmutableArray<Folder> Items { get; } = [.. items];
}

public sealed class Region(string name, params City[] cities)
{
    public string Name { get; } = name;

    public ImmutableArray<City> Cities { get; } = [.. cities];
}

public sealed class City(string name, params Street[] streets)
{
    public string Name { get; } = name;

    public ImmutableArray<Street> Streets { get; } = [.. streets];
}

public sealed record Street(string Name);

public sealed class Forest(ImmutableArray<Folder> folders, ImmutableArray<Region> regions)
{
    public ImmutableArray<Folder> Folders { get; } = folders;

    public ImmutableArray<Region> Regions { get; } = regions;

    public Folder? Picked => Folders.IsEmpty ? null : Folders[0];
}
