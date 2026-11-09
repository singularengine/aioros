namespace Aioros.Targets.Ssr.Hosting;

public sealed class SsrOptions
{
    public string Language { get; set; } = "en";

    public bool Development { get; set; }

    /// <summary>The token <c>aioros dev</c> gave the app (<c>Aioros:DevelopmentToken</c>), with which it posts the build's errors; none refuses them.</summary>
    public string? DevelopmentToken { get; set; }

    /// <summary>The process id of the tool that started the app (<c>Aioros:DevelopmentTool</c>), so the app stops when it is gone; none in production.</summary>
    public int? DevelopmentTool { get; set; }

    /// <summary>The installed app's URL scheme; each page then names it in <c>&lt;meta name="aioros-app"&gt;</c> for "open in the desktop app" links.</summary>
    public string? AppScheme { get; set; }

    /// <summary>The app's icon linked from every page's head (<see cref="Search.AppIcon"/>), or none.</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// The theme a visitor who has chosen none gets (<c>Aioros:Theme</c>): <c>light</c>, the default, <c>dark</c>, or
    /// <c>system</c> to follow the device's color scheme (<see cref="Themes.ThemeSwitch"/>).
    /// </summary>
    public string Theme { get; set; } = Themes.ThemeSwitch.Light;

    public bool ExternalCss { get; set; }

    /// <summary>The Content Security Policy written on HTML documents outside development (<see cref="Hosting.ContentSecurity"/>); empty turns it off.</summary>
    public string ContentSecurityPolicy { get; set; } = Hosting.ContentSecurity.DefaultPolicy;

    /// <summary>
    /// Whether forms carry ASP.NET Core's antiforgery token and posts must return it, on top of the cross-origin check;
    /// off by default (<see cref="PageServices.AddAntiforgeryTokens"/>).
    /// </summary>
    public bool Antiforgery { get; set; }

    public string HeadCss { get; set; } = string.Empty;

    /// <summary>How many bytes of answers the page cache keeps (<c>Aioros:PageCacheBytes</c>); 0 turns it off.</summary>
    public long PageCacheBytes { get; set; } = Caching.PageCache.DefaultMaxBytes;

    /// <summary>The largest file a post may upload, in bytes (<c>Aioros:UploadLimit</c>, 10 MB by default); a larger one answers 413.</summary>
    public long UploadLimit { get; set; } = DefaultUploadLimit;

    public const long DefaultUploadLimit = 10 * 1024 * 1024;

    public global::Aioros.Pages.PageDefinition? NotFound { get; set; }

    public global::Aioros.Pages.PageDefinition? Error { get; set; }

    /// <summary>The app's stand-in for native-only pages (<c>Pages/NativeOnly.axaml</c>); without it the built-in one is written.</summary>
    public global::Aioros.Pages.PageDefinition? NativeOnly { get; set; }

    /// <summary>Where the stand-in's "Download" link points (<c>Aioros:Download</c>).</summary>
    public string Download { get; set; } = "/download";
}
