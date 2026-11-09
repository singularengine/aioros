namespace Aioros.Targets.Ssr.Live;

internal sealed class EventBuffer(Stream inner) : Stream
{
    private readonly MemoryStream _pending = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _pending.Length;

    public override long Position
    {
        get => _pending.Position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => _pending.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _pending.WriteAsync(buffer, cancellationToken);

    public override void Flush() => throw new NotSupportedException("Flush asynchronously.");

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pending.Length == 0)
        {
            return;
        }

        await inner.WriteAsync(_pending.GetBuffer().AsMemory(0, (int)_pending.Length), cancellationToken);
        _pending.SetLength(0);
        await inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        await FlushAsync(CancellationToken.None);
        await _pending.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pending.Dispose();
        }

        base.Dispose(disposing);
    }
}
