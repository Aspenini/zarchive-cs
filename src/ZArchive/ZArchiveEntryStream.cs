namespace ZArchive;

/// <summary>
/// Read-only, seekable stream over one archived file, backed by the native
/// random-access read API. Reads are synchronous native operations; the
/// inherited asynchronous Stream methods wrap these synchronous reads.
/// </summary>
internal sealed class ZArchiveEntryStream : Stream
{
    private readonly ZArchiveReader _reader;
    private readonly uint _node;
    private readonly long _length;
    private readonly string _archivePath;
    private long _position;
    private bool _disposed;

    internal ZArchiveEntryStream(ZArchiveReader reader, uint node, long length, string archivePath)
    {
        _reader = reader;
        _node = node;
        _length = length;
        _archivePath = archivePath;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => !_disposed;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _length;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _position;
        }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        _reader.ThrowIfDisposed();
        if (buffer.IsEmpty || _position >= _length)
            return 0;
        long remaining = _length - _position;
        if (buffer.Length > remaining)
            buffer = buffer[..(int)Math.Min(buffer.Length, remaining)];
        int read = _reader.ReadFileAt(_node, (ulong)_position, buffer, _archivePath);
        _position += read;
        return read;
    }

    public override int ReadByte()
    {
        Span<byte> single = stackalloc byte[1];
        return Read(single) == 1 ? single[0] : -1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (target < 0)
            throw new IOException("Attempted to seek before the beginning of the stream.");
        _position = target;
        return _position;
    }

    public override void Flush()
    {
        // Read-only stream: nothing to flush.
    }

    public override void SetLength(long value) =>
        throw new NotSupportedException("The stream is read-only.");

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is read-only.");

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
