namespace ZArchive;

/// <summary>
/// Write-only stream for a single archive entry. Data is forwarded to the
/// native append-only writer; the stream cannot seek or read. Disposing the
/// stream closes the logical entry.
/// </summary>
internal sealed class ZArchiveWriterEntryStream : Stream
{
    private readonly ZArchiveWriter _writer;
    private long _position;
    private bool _disposed;

    internal ZArchiveWriterEntryStream(ZArchiveWriter writer)
    {
        _writer = writer;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed;

    public override long Length => _position;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException("The stream is append-only.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty)
            return;
        _writer.AppendToActiveEntry(buffer);
        _position += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        ReadOnlySpan<byte> single = [value];
        Write(single);
    }

    public override void Flush()
    {
        // Data is forwarded to the native writer immediately.
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is write-only.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("The stream is append-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("The stream is append-only.");

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
                _writer.OnEntryStreamClosed(this);
        }
        base.Dispose(disposing);
    }
}
