using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

public sealed class Stylesheets
{
    public const string Path = "/_aioros/css/";

    private ImmutableDictionary<string, byte[]> _files = ImmutableDictionary<string, byte[]>.Empty.WithComparers(StringComparer.Ordinal);

    public string Add(string css)
    {
        var bytes = Encoding.UTF8.GetBytes(css);
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes).AsSpan(0, 8)) + ".css";
        ImmutableInterlocked.TryAdd(ref _files, name, bytes);
        return Path + name;
    }

    public bool TryGet(string name, out byte[] css) => _files.TryGetValue(name, out css!);

    public static async Task ServeAsync(HttpContext http, string name, Stylesheets styles)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(styles);
        if (!styles.TryGet(name, out var css))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        http.Response.ContentType = "text/css; charset=utf-8";
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        await http.Response.Body.WriteAsync(css, http.RequestAborted);
    }
}
