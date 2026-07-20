using Xunit;

namespace ZArchive.Tests;

public class WriterTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void OnlyOneEntryStreamMayBeOpen()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("one-entry.zar"));
        using Stream first = writer.CreateEntry("a.bin");
        Assert.Throws<InvalidOperationException>(() => writer.CreateEntry("b.bin"));
    }

    [Fact]
    public void CompleteWithOpenEntryStreamThrows()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("open-entry.zar"));
        using Stream entry = writer.CreateEntry("a.bin");
        Assert.Throws<InvalidOperationException>(writer.Complete);
    }

    [Fact]
    public void CompleteMayOnlyBeCalledOnce()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("double-complete.zar"));
        using (Stream entry = writer.CreateEntry("a.bin"))
            entry.Write("data"u8);
        writer.Complete();
        Assert.Throws<InvalidOperationException>(writer.Complete);
        Assert.Throws<InvalidOperationException>(() => writer.CreateEntry("b.bin"));
    }

    [Fact]
    public void DuplicateEntryPathThrows()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("duplicate.zar"));
        using (Stream entry = writer.CreateEntry("a.bin"))
            entry.Write("data"u8);
        Assert.Throws<InvalidOperationException>(() => writer.CreateEntry("a.bin"));
    }

    [Fact]
    public void EntryStreamIsWriteOnly()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("write-only.zar"));
        using Stream entry = writer.CreateEntry("a.bin");
        Assert.True(entry.CanWrite);
        Assert.False(entry.CanRead);
        Assert.False(entry.CanSeek);
        Assert.Throws<NotSupportedException>(() => entry.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => entry.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => entry.SetLength(1));
        Assert.Throws<NotSupportedException>(() => entry.Position = 1);
    }

    [Fact]
    public void ClosedEntryStreamRejectsWrites()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("closed-entry.zar"));
        Stream entry = writer.CreateEntry("a.bin");
        entry.Dispose();
        Assert.Throws<ObjectDisposedException>(() => entry.Write("x"u8));
    }

    [Fact]
    public void DisposeWithoutCompleteDeletesOutputFile()
    {
        string archive = _dir.File("abandoned.zar");
        using (ZArchiveWriter writer = ZArchiveWriter.Create(archive))
        {
            using Stream entry = writer.CreateEntry("a.bin");
            entry.Write(TestUtil.DeterministicBytes(100_000));
        }
        Assert.False(File.Exists(archive));
    }

    [Fact]
    public void CompletedArchiveSurvivesDispose()
    {
        string archive = _dir.File("kept.zar");
        using (ZArchiveWriter writer = ZArchiveWriter.Create(archive))
        {
            using (Stream entry = writer.CreateEntry("a.bin"))
                entry.Write("data"u8);
            writer.Complete();
        }
        Assert.True(File.Exists(archive));
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.True(reader.FileExists("a.bin"));
    }

    [Fact]
    public void CreateEntryCreatesParentDirectoriesAutomatically()
    {
        string archive = _dir.File("autodirs.zar");
        using (ZArchiveWriter writer = ZArchiveWriter.Create(archive))
        {
            using (Stream entry = writer.CreateEntry("a/b/c/deep.bin"))
                entry.Write("deep"u8);
            writer.Complete();
        }
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.True(reader.DirectoryExists("a/b/c"));
        Assert.True(reader.FileExists("a/b/c/deep.bin"));
    }

    [Fact]
    public void NonRecursiveCreateDirectoryRequiresParent()
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("nonrecursive.zar"));
        Assert.Throws<InvalidOperationException>(
            () => writer.CreateDirectory("missing/child", recursive: false));
        writer.CreateDirectory("missing/child"); // recursive default succeeds
    }

    [Fact]
    public void AddFileAndAddDirectoryRoundTrip()
    {
        string sourceDir = _dir.File("source");
        Directory.CreateDirectory(Path.Combine(sourceDir, "sub"));
        byte[] fileA = TestUtil.DeterministicBytes(1000, seed: 10);
        byte[] fileB = TestUtil.DeterministicBytes(200_000, seed: 11);
        File.WriteAllBytes(Path.Combine(sourceDir, "a.bin"), fileA);
        File.WriteAllBytes(Path.Combine(sourceDir, "sub", "b.bin"), fileB);

        string archive = _dir.File("adddir.zar");
        using (ZArchiveWriter writer = ZArchiveWriter.Create(archive))
        {
            writer.AddDirectory(sourceDir, "content");
            writer.Complete();
        }

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        using var memory = new MemoryStream();
        reader.OpenRead("content/sub/b.bin").CopyTo(memory);
        Assert.Equal(TestUtil.Sha256(fileB), TestUtil.Sha256(memory.ToArray()));
        ZArchiveEntry? entryA = reader.GetEntry("content/a.bin");
        Assert.NotNull(entryA);
        Assert.Equal(fileA.Length, entryA.Length);
    }

    [Fact]
    public void DisposedWriterThrowsOnUse()
    {
        ZArchiveWriter writer = ZArchiveWriter.Create(_dir.File("disposed.zar"));
        writer.Dispose();
        writer.Dispose(); // double dispose is harmless
        Assert.Throws<ObjectDisposedException>(() => writer.CreateEntry("a.bin"));
        Assert.Throws<ObjectDisposedException>(writer.Complete);
    }
}
