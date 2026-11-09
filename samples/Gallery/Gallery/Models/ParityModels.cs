using System.Collections.Immutable;
using System.Globalization;
using Gallery.Parity;

namespace Gallery.Models;

public sealed record ParityScores(string Name, string Href, string Texts, string Layout, string Colors, string Pixels)
{
    public static ParityScores Of(string name, string href, IReadOnlyCollection<ParityVariant> variants) => new(
        name,
        href,
        Percent(variants.Average(variant => variant.Texts)),
        Percent(variants.Average(variant => variant.Layout)),
        Percent(variants.Average(variant => variant.Colors)),
        Percent(variants.Average(variant => variant.Pixels)));

    private static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
}

public sealed record ParityOverview(string Measured, ImmutableArray<ParityScores> Families, ParityScores All);

public sealed record ParityFamily(string Title, string Live, string Measured, string Avalonia, string Web, string Differences, ImmutableArray<ParityScores> Variants);

public sealed class ParityModel(ParityOverview overview)
{
    public string Measured { get; } = overview.Measured;

    public ImmutableArray<ParityScores> Families { get; } = overview.Families;

    public ParityScores All { get; } = overview.All;
}

public sealed class ParityFamilyModel(ParityFamily family)
{
    public string Title { get; } = family.Title;

    public string Live { get; } = family.Live;

    public string Measured { get; } = family.Measured;

    public string Avalonia { get; } = family.Avalonia;

    public string Web { get; } = family.Web;

    public string Differences { get; } = family.Differences;

    public ImmutableArray<ParityScores> Variants { get; } = family.Variants;
}
