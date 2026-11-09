using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The page inside its layouts, as marked in a rendered body, and a signature of everything around it, so a navigation
/// whose layouts render the same can send the page alone.
/// </summary>
public static class PageRegion
{
    public const string Open = "<div data-aioros-page style=\"display:contents\"><!--p-->";

    public const string Close = "<!--/p--></div>";

    private static readonly byte[] OpenUtf8 = Encoding.UTF8.GetBytes(Open);
    private static readonly byte[] CloseUtf8 = Encoding.UTF8.GetBytes(Close);
    private static readonly byte[] CurrentUtf8 = " aria-current=\"page\""u8.ToArray();
    private static readonly byte[] CurrentTail = "-current=\"page\""u8.ToArray();

    /// <summary>The signature of the layouts and the range of the page inside a body, or nothing when the body has no page mark.</summary>
    public static (string Signature, Range Page)? Of(ReadOnlySpan<byte> body)
    {
        var open = body.IndexOf(OpenUtf8);
        var close = body.LastIndexOf(CloseUtf8);
        if (open < 0 || close < open)
        {
            return null;
        }

        var signature = new Signature();
        signature.Add(body[..open]);
        signature.Add(body[close..]);
        return (signature.Finish().ToString("x16", CultureInfo.InvariantCulture), (open + OpenUtf8.Length)..close);
    }

    /// <summary>
    /// A hash of the bytes outside the page with every <c>aria-current</c> mark left out, as if they were one stream: it
    /// reads them 32 at a time in four independent lanes (xxHash64's stripes), carrying the bytes between marks over, so
    /// where a mark falls never changes it.
    /// </summary>
    private ref struct Signature()
    {
        private const ulong Prime1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
        private const int StripeLength = 4 * sizeof(ulong);
        private ulong _lane1 = 0x27D4EB2F165667C5UL;
        private ulong _lane2 = Prime2;
        private ulong _lane3;
        private ulong _lane4 = Prime1;
        private ulong _pending;
        private int _count;
        private int _lane;
        private long _length;

        public void Add(ReadOnlySpan<byte> bytes)
        {
            while (!bytes.IsEmpty)
            {
                var mark = Mark(bytes);
                Stream(mark < 0 ? bytes : bytes[..mark]);
                bytes = mark < 0 ? [] : bytes[(mark + CurrentUtf8.Length)..];
            }
        }

        public readonly ulong Finish()
        {
            var hash = BitOperations.RotateLeft(_lane1, 1) + BitOperations.RotateLeft(_lane2, 7) + BitOperations.RotateLeft(_lane3, 12) + BitOperations.RotateLeft(_lane4, 18);
            hash = _count == 0 ? hash : Mix(hash, _pending);
            hash ^= (ulong)_length;
            hash = (hash ^ (hash >> 33)) * Prime2;
            return hash ^ (hash >> 29);
        }

        private void Stream(ReadOnlySpan<byte> bytes)
        {
            _length += bytes.Length;
            while ((_count != 0 || _lane != 0) && !bytes.IsEmpty)
            {
                Take(bytes[0]);
                bytes = bytes[1..];
            }

            while (bytes.Length >= StripeLength)
            {
                _lane1 = Mix(_lane1, BinaryPrimitives.ReadUInt64LittleEndian(bytes));
                _lane2 = Mix(_lane2, BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]));
                _lane3 = Mix(_lane3, BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]));
                _lane4 = Mix(_lane4, BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]));
                bytes = bytes[StripeLength..];
            }

            foreach (var value in bytes)
            {
                Take(value);
            }
        }

        private void Take(byte value)
        {
            _pending |= (ulong)value << (_count * 8);
            if (++_count < sizeof(ulong))
            {
                return;
            }

            switch (_lane)
            {
                case 0: _lane1 = Mix(_lane1, _pending); break;
                case 1: _lane2 = Mix(_lane2, _pending); break;
                case 2: _lane3 = Mix(_lane3, _pending); break;
                default: _lane4 = Mix(_lane4, _pending); break;
            }

            (_pending, _count, _lane) = (0, 0, (_lane + 1) & 3);
        }

        /// <summary>
        /// Where the first mark starts: found by its tail, whose first and last bytes are rare in markup, where a search
        /// for the whole mark would stop at every space followed by a quote.
        /// </summary>
        private static int Mark(ReadOnlySpan<byte> bytes)
        {
            var head = CurrentUtf8.Length - CurrentTail.Length;
            for (var from = 0; ;)
            {
                var tail = bytes[from..].IndexOf(CurrentTail);
                if (tail < 0)
                {
                    return -1;
                }

                var start = from + tail - head;
                if (start >= 0 && bytes.Slice(start, CurrentUtf8.Length).SequenceEqual(CurrentUtf8))
                {
                    return start;
                }

                from += tail + 1;
            }
        }

        private static ulong Mix(ulong hash, ulong word) => BitOperations.RotateLeft(hash ^ (word * Prime2), 31) * Prime1;
    }
}
