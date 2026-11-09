using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

internal sealed class ScriptModule
{
    public ScriptModule(string prefix, string source)
    {
        Bytes = Encoding.UTF8.GetBytes(source);
        Path = prefix + Convert.ToHexStringLower(SHA256.HashData(Bytes.Span).AsSpan(0, 8)) + ".js";
        Name = Path[prefix.Length..];
    }

    public ReadOnlyMemory<byte> Bytes { get; }

    public string Name { get; }

    public string Path { get; }

    public async Task ServeAsync(HttpContext http, string name)
    {
        if (!string.Equals(name, Name, StringComparison.Ordinal))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        http.Response.ContentType = "text/javascript; charset=utf-8";
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        await http.Response.Body.WriteAsync(Bytes, http.RequestAborted);
    }
}
