using System.Buffers;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// A buffer for a rendered body or region, rented from the shared array pool and returned when disposed: a page of tens
/// of kilobytes would otherwise allocate every array it grows through, the last ones on the large object heap.
/// </summary>
public sealed class PooledBuffer(int capacity = PooledBuffer.DefaultCapacity) : IBufferWriter<byte>, IDisposable
{
    /// <summary>The capacity rented first: a page larger than this grows once or twice, through pooled arrays too.</summary>
    public const int DefaultCapacity = 128 * 1024;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(capacity);
    private int _written;

    /// <summary>The bytes written so far.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    /// <summary>The bytes written so far, valid until the buffer is disposed.</summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    /// <summary>Forgets the bytes written so far, keeping the rented array for the next ones.</summary>
    public void ResetWrittenCount() => _written = 0;

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _buffer.Length - _written);
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsSpan(_written);
    }

    public void Dispose()
    {
        var buffer = _buffer;
        (_buffer, _written) = ([], 0);
        if (buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void Ensure(int sizeHint)
    {
        var needed = _written + Math.Max(sizeHint, 1);
        if (needed <= _buffer.Length)
        {
            return;
        }

        var larger = ArrayPool<byte>.Shared.Rent(Math.Max(needed, _buffer.Length * 2));
        _buffer.AsSpan(0, _written).CopyTo(larger);
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        _buffer = larger;
    }
}
