using Xunit;

namespace ZArchive.Tests;

public class ReaderTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void OpenMissingFileThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => ZArchiveReader.Open(_dir.File("does-not-exist.zar")));
    }

    [Fact]
    public void OpenInvalidArchiveThrowsInvalidData()
    {
        string bogus = _dir.File("bogus.zar");
        File.WriteAllBytes(bogus, TestUtil.DeterministicBytes(4096));
        Assert.Throws<InvalidDataException>(() => ZArchiveReader.Open(bogus));
    }

    [Fact]
    public void TruncatedArchiveThrowsInvalidData()
    {
        string archive = _dir.File("truncated.zar");
        TestUtil.CreateSampleArchive(archive);
        byte[] bytes = File.ReadAllBytes(archive);
        File.WriteAllBytes(archive, bytes.AsSpan(0, bytes.Length / 2).ToArray());
        Assert.Throws<InvalidDataException>(() => ZArchiveReader.Open(archive));
    }

    [Fact]
    public void GetEntryReturnsNullForMissingPath()
    {
        string archive = _dir.File("missing.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Null(reader.GetEntry("no/such/entry.bin"));
        Assert.False(reader.TryGetEntry("nope.txt", out ZArchiveEntry? entry));
        Assert.Null(entry);
        Assert.False(reader.FileExists("emptydir")); // directory, not file
        Assert.False(reader.DirectoryExists("hello.txt")); // file, not directory
    }

    [Fact]
    public void OpenReadOnDirectoryThrows()
    {
        string archive = _dir.File("dirread.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<UnauthorizedAccessException>(() => reader.OpenRead("emptydir"));
    }

    [Fact]
    public void EnumerateMissingDirectoryThrows()
    {
        string archive = _dir.File("enum-missing.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<DirectoryNotFoundException>(
            () => reader.EnumerateEntries("no-such-dir").ToList());
    }

    [Fact]
    public void DisposedReaderThrowsOnUse()
    {
        string archive = _dir.File("disposed.zar");
        TestUtil.CreateSampleArchive(archive);
        ZArchiveReader reader = ZArchiveReader.Open(archive);
        reader.Dispose();
        reader.Dispose(); // double dispose is harmless
        Assert.Throws<ObjectDisposedException>(() => reader.GetEntry("hello.txt"));
        Assert.Throws<ObjectDisposedException>(() => reader.OpenRead("hello.txt"));
    }

    [Fact]
    public void StreamFromDisposedReaderThrowsOnRead()
    {
        string archive = _dir.File("stream-disposed.zar");
        TestUtil.CreateSampleArchive(archive);
        ZArchiveReader reader = ZArchiveReader.Open(archive);
        Stream stream = reader.OpenRead("hello.txt");
        reader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[16], 0, 16));
        stream.Dispose();
    }

    [Fact]
    public void PathsWithNulCharactersAreRejected()
    {
        string archive = _dir.File("nul.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<ArgumentException>(() => reader.GetEntry("bad\0path"));
    }
}
