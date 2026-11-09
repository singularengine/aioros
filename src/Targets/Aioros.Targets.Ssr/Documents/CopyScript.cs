using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The copy module of the framework's script, loaded only by pages holding a button that copies a text
/// (<c>data-aioros-copies</c>, Aioros.Cosmo's code block copy button): a click writes the button's text to the visitor's
/// clipboard with <c>navigator.clipboard.writeText</c>, with no request, and marks the button <c>data-copied</c> for two
/// seconds after the last copy, which gives it the looks its theme gives it under <c>:copied</c>, as shadcn/ui's copy
/// buttons show their check. It only writes the clipboard: reading it is not offered.
/// </summary>
public static class CopyScript
{
    public const string Prefix = "/_aioros/copy/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("copy"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
