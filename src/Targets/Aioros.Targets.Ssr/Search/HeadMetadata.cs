using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Targets.Ssr.Search;

public readonly record struct HeadMetadata(string Title, PageMetadata Metadata, string Origin, string Path)
{
    public void Write(ref HtmlOutput output)
    {
        if (!Metadata.Indexable)
        {
            output.WriteUtf8("<meta name=\"robots\" content=\"noindex\">\n"u8);
        }

        foreach (var alternate in Metadata.Alternates.IsDefault ? [] : Metadata.Alternates)
        {
            output.WriteUtf8("<link rel=\"alternate\" hreflang=\""u8);
            output.WriteText(alternate.Language);
            output.WriteUtf8("\" href=\""u8);
            output.WriteText(Absolute(alternate.Location));
            output.WriteUtf8("\">\n"u8);
        }

        if (Metadata.Description is null && Metadata.Canonical is null && Metadata.Image is null)
        {
            return;
        }

        var url = Absolute(Metadata.Canonical ?? Path);
        Tag(ref output, "<meta name=\"description\" content=\"", Metadata.Description);
        Tag(ref output, "<link rel=\"canonical\" href=\"", Metadata.Canonical is null ? null : url);
        Tag(ref output, "<meta property=\"og:title\" content=\"", Title);
        Tag(ref output, "<meta property=\"og:description\" content=\"", Metadata.Description);
        Tag(ref output, "<meta property=\"og:url\" content=\"", url);
        Tag(ref output, "<meta property=\"og:image\" content=\"", Metadata.Image is null ? null : Absolute(Metadata.Image));
        Tag(ref output, "<meta property=\"og:type\" content=\"", Metadata.Type);
        Tag(ref output, "<meta name=\"twitter:card\" content=\"", Metadata.Image is null ? "summary" : "summary_large_image");
    }

    private string Absolute(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http", StringComparison.Ordinal)
            ? absolute.ToString()
            : Origin + (location.StartsWith('/') ? location : "/" + location);

    private static void Tag(ref HtmlOutput output, string open, string? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteRaw(open);
        output.WriteText(value);
        output.WriteUtf8("\">\n"u8);
    }
}
