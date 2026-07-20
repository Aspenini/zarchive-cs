using Xunit;

namespace ZArchive.Tests;

public class StreamTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly ZArchiveReader _reader;
    private readonly Dictionary<string, byte[]> _files;

    public StreamTests()
    {
        string archive = _dir.File("streams.zar");
        _files = TestUtil.CreateSampleArchive(archive);
        _reader = ZArchiveReader.Open(archive);
    }

    public void Dispose()
    {
        _reader.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void ReportsCorrectCapabilitiesAndLength()
    {
        using Stream stream = _reader.OpenRead("data/nested/large.bin");
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(_files["data/nested/large.bin"].Length, stream.Length);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void RandomAccessReadsAcrossBlockBoundaryMatchSource()
    {
        byte[] expected = _files["data/nested/large.bin"];
        using Stream stream = _reader.OpenRead("data/nested/large.bin");

        // Read a window straddling the 64 KiB compression block boundary.
        stream.Seek(64 * 1024 - 100, SeekOrigin.Begin);
        byte[] window = new byte[200];
        stream.ReadExactly(window);
        Assert.Equal(expected.AsSpan(64 * 1024 - 100, 200).ToArray(), window);
        Assert.Equal(64 * 1024 + 100, stream.Position);
    }

    [Fact]
    public void SeekFromAllOriginsWorks()
    {
        using Stream stream = _reader.OpenRead("hello.txt");
        long length = stream.Length;
        Assert.Equal(2, stream.Seek(2, SeekOrigin.Begin));
        Assert.Equal(5, stream.Seek(3, SeekOrigin.Current));
        Assert.Equal(length - 1, stream.Seek(-1, SeekOrigin.End));
        Assert.Equal((int)'!', stream.ReadByte());
        Assert.Equal(-1, stream.ReadByte()); // at end
    }

    [Fact]
    public void NegativeSeekThrows()
    {
        using Stream stream = _reader.OpenRead("hello.txt");
        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek(-100, SeekOrigin.End));
    }

    [Fact]
    public void SeekBeyondEndReadsZeroBytes()
    {
        using Stream stream = _reader.OpenRead("hello.txt");
        stream.Seek(1000, SeekOrigin.Begin);
        Assert.Equal(0, stream.Read(new byte[10], 0, 10));
    }

    [Fact]
    public void ZeroByteReadSucceeds()
    {
        using Stream stream = _reader.OpenRead("hello.txt");
        Assert.Equal(0, stream.Read(Array.Empty<byte>(), 0, 0));
    }

    [Fact]
    public void EmptyFileReadsZeroBytes()
    {
        using Stream stream = _reader.OpenRead("empty.bin");
        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Read(new byte[10], 0, 10));
    }

    [Fact]
    public void WriteOperationsAreNotSupported()
    {
        using Stream stream = _reader.OpenRead("hello.txt");
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public void DisposedStreamThrows()
    {
        Stream stream = _reader.OpenRead("hello.txt");
        stream.Dispose();
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
    }
}
