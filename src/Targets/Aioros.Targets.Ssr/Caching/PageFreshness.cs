using System.Globalization;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Caching;

/// <summary>How a page answer may be kept by browsers and shared caches, after RFC 9111's <c>Cache-Control</c>.</summary>
public enum PageKind
{
    /// <summary>A post's answer or an error page: <c>no-store</c> and no tag.</summary>
    Unstored,

    /// <summary>A page that may depend on the user or its cookies: <c>private, no-cache</c>, kept only by the browser and revalidated, keyed also by the cookies.</summary>
    Private,

    /// <summary>A page whose loaders declared it the same for every visitor: <c>public, no-cache</c>, keyed also by the cookies.</summary>
    Public,
}

/// <summary>
/// The validators of a page answer (RFC 9110 §8.8.3 and §13.1.2): a strong <c>ETag</c> that hashes the bytes written
/// with xxHash3, then the request's <c>Accept-Encoding</c>, so each content coding the compression middleware may apply
/// gets its own tag, and the answer to <c>If-None-Match</c>, whose comparison is weak and accepts <c>*</c>.
/// </summary>
public static class PageFreshness
{
    public const string PublicControl = "public, no-cache";

    public const string PrivateControl = "private, no-cache";

    public const string UnstoredControl = "no-store";

    /// <summary>The hash of an answer's bytes, written in up to three parts, as one stream would hash them.</summary>
    public static ulong Hash(ReadOnlySpan<byte> start, ReadOnlySpan<byte> content = default, ReadOnlySpan<byte> tail = default)
    {
        var hash = XxHash3.HashToUInt64(start);
        hash = XxHash3.HashToUInt64(content, unchecked((long)hash));
        return XxHash3.HashToUInt64(tail, unchecked((long)hash));
    }

    /// <summary>The quoted tag of an answer whose bytes hash to <paramref name="hash"/>, for this request's content codings.</summary>
    public static string Tag(HttpRequest request, ulong hash)
    {
        ArgumentNullException.ThrowIfNull(request);
        var encodings = request.Headers.AcceptEncoding;
        foreach (var encoding in encodings)
        {
            hash = XxHash3.HashToUInt64(MemoryMarshal.AsBytes((encoding ?? string.Empty).AsSpan()), unchecked((long)hash));
        }

        return string.Create(CultureInfo.InvariantCulture, $"\"{hash:x16}\"");
    }

    /// <summary>Sets the validators and the <c>Cache-Control</c> of an answer of this kind.</summary>
    public static void Describe(HttpResponse response, PageKind kind, string? tag)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.CacheControl = kind switch
        {
            PageKind.Public => PublicControl,
            PageKind.Private => PrivateControl,
            _ => UnstoredControl,
        };
        if (tag is not null)
        {
            response.Headers.ETag = tag;
        }

        if (kind is PageKind.Public or PageKind.Private)
        {
            response.Headers.Vary = response.Headers.Vary.Count == 0 ? "Cookie" : response.Headers.Vary + ", Cookie";
        }
    }

    /// <summary>Whether a <c>GET</c> request's <c>If-None-Match</c> names the tag (weakly) or is <c>*</c>, so it is answered <c>304</c>.</summary>
    public static bool NotModified(HttpRequest request, string tag)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        foreach (var value in request.Headers.IfNoneMatch)
        {
            foreach (var range in (value ?? string.Empty).AsSpan().Split(','))
            {
                var candidate = value.AsSpan()[range].Trim();
                if (candidate.StartsWith("W/", StringComparison.Ordinal))
                {
                    candidate = candidate[2..];
                }

                if (candidate.SequenceEqual("*") || candidate.SequenceEqual(tag))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Answers <c>304</c>: the validators and caching headers stay, the body and its type go.</summary>
    public static void AnswerNotModified(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode = StatusCodes.Status304NotModified;
        response.ContentType = null;
        response.ContentLength = null;
        response.Headers.Remove(Pages.PageHandler.PartialHeader);
    }
}
