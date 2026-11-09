using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

public static class PointerScript
{
    public const string Prefix = "/_aioros/pointer/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("pointer"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
