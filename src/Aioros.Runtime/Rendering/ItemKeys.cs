using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The key of a list item in the names a page writes: the field's token, a <c>~</c> and 24 hexadecimal digits, 96 bits of SHA-256,
/// of the item's key (<c>f1~1a2b3c4d5e6f708192a3b4c5</c>), so a post says which item it is for and is refused when another item now stands
/// there (React's <c>key</c>, LiveView's stream ids). A nested list's key extends its outer item's.
/// </summary>
public static class ItemKeys
{
    public const char Mark = '~';

    private const int Digits = 24;
    private const int ShapeDigits = 8;
    public const uint Basis = 2166136261;
    private const uint Prime = 16777619;
    private const uint NoIdentity = 2654435769;

    private static readonly UInt128 KeyBits = (UInt128.One << (Digits * 4)) - 1;

    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");

    /// <summary>The key of the item with this key value inside <paramref name="outer"/>, or <paramref name="outer"/> when the value is null.</summary>
    public static UInt128? Combine(UInt128? outer, object? value)
    {
        if (value is null)
        {
            return outer;
        }

        Span<char> formatted = stackalloc char[64];
        var text = value is ISpanFormattable span && span.TryFormat(formatted, out var written, default, CultureInfo.InvariantCulture)
            ? formatted[..written]
            : (value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).AsSpan();
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(MemoryMarshal.AsBytes(text), digest);
        if (outer is { } around)
        {
            Span<byte> both = stackalloc byte[(sizeof(ulong) * 2) + SHA256.HashSizeInBytes];
            BinaryPrimitives.WriteUInt128LittleEndian(both, around);
            digest.CopyTo(both[(sizeof(ulong) * 2)..]);
            SHA256.HashData(both, digest);
        }

        return BinaryPrimitives.ReadUInt128LittleEndian(digest) & KeyBits;
    }

    /// <summary>A list's shape with one more item: the item's key, or a constant for an item without an identity, so the shape follows the items' count.</summary>
    public static uint Mix(uint shape, UInt128? key) => (shape ^ (key is { } value ? (uint)value : NoIdentity)) * Prime;

    /// <summary>The value of a region's shape mark: its digest and the id of the first field written in a list, which nothing before it can shift.</summary>
    public static string ShapeText(uint shape, int firstListed) =>
        shape.ToString("x8", CultureInfo.InvariantCulture) + "." + (firstListed < 0 ? string.Empty : firstListed.ToString(CultureInfo.InvariantCulture));

    /// <summary>A shape mark's digest.</summary>
    public static ReadOnlySpan<char> ShapeDigest(string? mark) => mark is null ? Basis.ToString("x8", CultureInfo.InvariantCulture) : mark.AsSpan(0, Math.Min(ShapeDigits, mark.Length));

    /// <summary>The id of the first field written in a list according to a shape mark, or the largest id when it wrote none.</summary>
    public static int ShapeFirst(string? mark) =>
        mark is not null && mark.Length > ShapeDigits + 1 && int.TryParse(mark.AsSpan(ShapeDigits + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var first) ? first : int.MaxValue;

    /// <summary>What a name carries after its token for this key: nothing without one.</summary>
    public static string Suffix(UInt128? key) => key is { } value ? Mark + value.ToString("x24", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Whether a posted token ends in an item's key.</summary>
    public static bool Keyed(ReadOnlySpan<char> token) =>
        token.Length > Digits && token[^(Digits + 1)] == Mark && token[^Digits..].IndexOfAnyExcept(HexDigits) < 0;

    /// <summary>A posted token without its item's key.</summary>
    public static ReadOnlySpan<char> Bare(ReadOnlySpan<char> token) => Keyed(token) ? token[..^(Digits + 1)] : token;
}
