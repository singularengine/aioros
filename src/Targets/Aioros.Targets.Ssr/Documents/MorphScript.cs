using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

public static class MorphScript
{
    public const string Prefix = "/_aioros/morph/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("morph"));

    public static ReadOnlyMemory<byte> Bytes => Module.Bytes;

    public static string Name => Module.Name;

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
