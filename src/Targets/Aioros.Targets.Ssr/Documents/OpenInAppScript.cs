using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The native-only stand-in's "open in the app" script (<see cref="Scripts.OpenInApp"/>) as a file, loaded only by that
/// page, so a Content Security Policy without inline scripts holds on it too.
/// </summary>
public static class OpenInAppScript
{
    public const string Prefix = "/_aioros/open/";

    private static readonly ScriptModule Module = new(Prefix, Scripts.OpenInApp);

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
