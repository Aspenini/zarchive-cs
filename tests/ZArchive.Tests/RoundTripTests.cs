using Xunit;

namespace ZArchive.Tests;

public class RoundTripTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void WrittenArchiveCanBeReadBackWithIdenticalContent()
    {
        string archive = _dir.File("roundtrip.zar");
        Dictionary<string, byte[]> files = TestUtil.CreateSampleArchive(archive);

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        foreach ((string path, byte[] expected) in files)
        {
            ZArchiveEntry? entry = reader.GetEntry(path);
            Assert.NotNull(entry);
            Assert.True(entry.IsFile);
            Assert.Equal(expected.Length, entry.Length);

            using Stream stream = reader.OpenRead(path);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Assert.Equal(TestUtil.Sha256(expected), TestUtil.Sha256(memory.ToArray()));
        }
    }

    [Fact]
    public void EnumerationReturnsAllEntriesWithCorrectTypes()
    {
        string archive = _dir.File("enumerate.zar");
        Dictionary<string, byte[]> files = TestUtil.CreateSampleArchive(archive);

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        List<ZArchiveEntry> entries = reader.EnumerateEntries(recursive: true).ToList();

        foreach (string path in files.Keys)
            Assert.Contains(entries, e => e.FullName == path && e.IsFile);
        Assert.Contains(entries, e => e.FullName == "emptydir" && e.IsDirectory);
        Assert.Contains(entries, e => e.FullName == "data/nested" && e.IsDirectory);

        List<ZArchiveEntry> topLevel = reader.EnumerateEntries().ToList();
        Assert.Contains(topLevel, e => e.FullName == "hello.txt");
        Assert.DoesNotContain(topLevel, e => e.FullName.Contains('/'));

        List<ZArchiveEntry> nested = reader.EnumerateEntries("data/nested").ToList();
        ZArchiveEntry large = Assert.Single(nested);
        Assert.Equal("large.bin", large.Name);
        Assert.Equal("data/nested/large.bin", large.FullName);
    }

    [Fact]
    public void RootEntryIsDirectory()
    {
        string archive = _dir.File("root.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.True(reader.Root.IsDirectory);
        Assert.Equal("", reader.Root.FullName);
    }

    [Fact]
    public void LookupIsCaseInsensitiveForLatinLetters()
    {
        string archive = _dir.File("case.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.True(reader.FileExists("HELLO.TXT"));
        Assert.True(reader.DirectoryExists("DATA/Nested"));
    }

    [Fact]
    public void BackslashesAndLeadingSlashAreNormalized()
    {
        string archive = _dir.File("slash.zar");
        TestUtil.CreateSampleArchive(archive);
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.True(reader.FileExists(@"data\nested\large.bin"));
        Assert.True(reader.FileExists("/hello.txt"));
    }

    [Fact]
    public void ConcurrentStreamsFromOneReaderReadCorrectly()
    {
        string archive = _dir.File("concurrent.zar");
        Dictionary<string, byte[]> files = TestUtil.CreateSampleArchive(archive);
        byte[] expected = files["data/nested/large.bin"];

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Parallel.For(0, 8, _ =>
        {
            using Stream stream = reader.OpenRead("data/nested/large.bin");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Assert.Equal(TestUtil.Sha256(expected), TestUtil.Sha256(memory.ToArray()));
        });
    }
}
