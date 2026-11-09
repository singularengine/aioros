using System.Text;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;

namespace Aioros.Targets.Ssr.Documents;

public sealed class DocumentParts
{
    private DocumentParts(byte[] head, byte[] css, byte[] afterTitle, byte[] tail, bool live, string plan)
    {
        NavigationAfterTitle = Encoding.UTF8.GetBytes("</title>\n" + (live ? LiveMeta : string.Empty) + plan);
        Head = head;
        AfterTitle = "</title>\n"u8.ToArray();
        Css = css;
        Start = afterTitle;
        Tail = tail;
    }

    public const string NavigationHeader = "X-Aioros-Navigation";

    private const string LiveMeta = "<meta name=\"aioros-live\" content=\"1\">\n";

    public static ReadOnlySpan<byte> NavigationHead => "<title>"u8;

    public static ReadOnlySpan<byte> NavigationTail => "</div></form>"u8;

    public byte[] NavigationAfterTitle { get; }

    public static ReadOnlySpan<byte> BodyOpen => "<div class=\"aioros-body\" style=\"display:contents\">"u8;

    /// <summary>The start of the document before its language, which each answer writes (<see cref="Hosting.LocalizedRequests"/>).</summary>
    public static ReadOnlySpan<byte> DocumentStart => "<!doctype html>\n<html lang=\""u8;

    /// <summary>The head from its start to the title, after <see cref="DocumentStart"/>, the language and <c>"&gt;</c>.</summary>
    public byte[] Head { get; }

    public byte[] AfterTitle { get; }

    /// <summary>The page's style sheets in the head, before the rule sets its slot renderers contribute.</summary>
    public byte[] Css { get; }

    /// <summary>The end of the head and the start of the body, up to the form's content.</summary>
    public byte[] Start { get; }

    public byte[] Tail { get; }

    public static DocumentParts For(CompiledPage page, SsrOptions options, bool live, Stylesheets? styles = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);
        var plan = page.Reusable ? "<meta name=\"aioros-plan\" content=\"" + page.Plan.Template.Fingerprint + "\">\n" : string.Empty;
        var head = "<head>\n<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n"
            + (live ? LiveMeta : string.Empty) + plan + AppMeta(options.AppScheme) + Search.AppIcon.Link(options.Icon) + Scripts.Tag + "<title>";
        var css = styles is null
            ? "<style data-css>\n" + BaseStyles.Css + options.HeadCss + page.Css + "</style>\n"
            : Link(styles.Add(BaseStyles.Css + options.HeadCss)) + Link(styles.Add(page.Css));
        var afterTitle = "</head>\n<body style=\"background:" + page.Background
            + ";color:" + page.Foreground + "\">\n<form method=\"post\"" + (page.Uploads ? " enctype=\"multipart/form-data\"" : string.Empty) + " novalidate class=\"c\" style=\"min-height:100vh\">";
        var tail = "</div></form>\n" + (options.Development ? Scripts.Development : string.Empty) + "</body></html>";
        return new DocumentParts(Encoding.UTF8.GetBytes(head), Encoding.UTF8.GetBytes(css), Encoding.UTF8.GetBytes(afterTitle), Encoding.UTF8.GetBytes(tail), live, plan);
    }

    internal static string AppMeta(string? scheme) =>
        scheme is { Length: > 0 } ? "<meta name=\"aioros-app\" content=\"" + System.Net.WebUtility.HtmlEncode(scheme) + "\">\n" : string.Empty;

    private static string Link(string href) => "<link rel=\"stylesheet\" href=\"" + href + "\" data-css>\n";
}
