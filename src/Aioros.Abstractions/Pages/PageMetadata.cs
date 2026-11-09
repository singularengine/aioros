using System.Collections.Immutable;

namespace Aioros.Pages;

public sealed record PageMetadata(
    string? Description = null,
    string? Canonical = null,
    string? Image = null,
    string Type = "website",
    bool Indexable = true,
    ImmutableArray<PageAlternate> Alternates = default)
{
    public static PageMetadata None { get; } = new();
}

/// <summary>The same page in another language: its BCP 47 tag (<c>es</c>, <c>zh-Hans</c>, <c>x-default</c>) and its path or URL, written as a <c>hreflang</c> alternate.</summary>
public sealed record PageAlternate(string Language, string Location);
