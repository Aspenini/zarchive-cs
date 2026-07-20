using Xunit;

namespace ZArchive.Tests;

public class ExtractionTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void ExtractToDirectoryReproducesAllFiles()
    {
        string archive = _dir.File("extract.zar");
        Dictionary<string, byte[]> files = TestUtil.CreateSampleArchive(archive);
        string destination = _dir.File("out");

        long reported = 0;
        using (ZArchiveReader reader = ZArchiveReader.Open(archive))
        {
            reader.ExtractToDirectory(destination, new ZArchiveExtractionOptions
            {
                Progress = new SynchronousProgress(p => reported = p.EntriesProcessed),
            });
        }

        foreach ((string path, byte[] expected) in files)
        {
            string extracted = Path.Combine(destination, path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(extracted), $"missing extracted file: {path}");
            Assert.Equal(TestUtil.Sha256(expected), TestUtil.Sha256File(extracted));
        }
        Assert.True(Directory.Exists(Path.Combine(destination, "emptydir")));
        Assert.True(reported > 0);
    }

    [Fact]
    public void ExtractFileHonorsOverwriteFlag()
    {
        string archive = _dir.File("overwrite.zar");
        TestUtil.CreateSampleArchive(archive);
        string destination = _dir.File("hello-copy.txt");

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        reader.ExtractFile("hello.txt", destination);
        Assert.Throws<IOException>(() => reader.ExtractFile("hello.txt", destination));
        reader.ExtractFile("hello.txt", destination, overwrite: true);
        Assert.Equal("Hello, ZArchive!", File.ReadAllText(destination));
    }

    [Fact]
    public void ExtractToDirectoryWithoutOverwriteFailsOnExistingFiles()
    {
        string archive = _dir.File("existing.zar");
        TestUtil.CreateSampleArchive(archive);
        string destination = _dir.File("out-existing");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "hello.txt"), "already here");

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<IOException>(() => reader.ExtractToDirectory(destination));
        reader.ExtractToDirectory(destination, new ZArchiveExtractionOptions { Overwrite = true });
        Assert.Equal("Hello, ZArchive!", File.ReadAllText(Path.Combine(destination, "hello.txt")));
    }

    [Fact]
    public void ExtractionRejectsTraversalEntries()
    {
        // The upstream writer happily stores ".." as an ordinary entry name,
        // which makes it possible to build a malicious archive with the
        // public API. Extraction must refuse to follow it.
        string archive = _dir.File("evil.zar");
        using (ZArchiveWriter writer = ZArchiveWriter.Create(archive))
        {
            using (Stream entry = writer.CreateEntry("../escape.txt"))
                entry.Write("evil"u8);
            writer.Complete();
        }

        string destination = _dir.File("safe-root");
        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<IOException>(() => reader.ExtractToDirectory(destination));
        Assert.False(File.Exists(_dir.File("escape.txt")));
    }

    [Fact]
    public void ExtractionIsCancellable()
    {
        string archive = _dir.File("cancel.zar");
        TestUtil.CreateSampleArchive(archive);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using ZArchiveReader reader = ZArchiveReader.Open(archive);
        Assert.Throws<OperationCanceledException>(() => reader.ExtractToDirectory(
            _dir.File("cancelled-out"),
            new ZArchiveExtractionOptions { CancellationToken = cancellation.Token }));
    }

    [Fact]
    public void ZArchiveFileRoundTripsADirectoryTree()
    {
        string sourceDir = _dir.File("tree");
        Directory.CreateDirectory(Path.Combine(sourceDir, "a", "b"));
        byte[] payload = TestUtil.DeterministicBytes(300_000, seed: 42);
        File.WriteAllBytes(Path.Combine(sourceDir, "a", "b", "payload.bin"), payload);
        File.WriteAllText(Path.Combine(sourceDir, "readme.txt"), "hi");

        string archive = _dir.File("tree.zar");
        ZArchiveFile.CreateFromDirectory(sourceDir, archive);
        string destination = _dir.File("tree-out");
        ZArchiveFile.ExtractToDirectory(archive, destination);

        Assert.Equal("hi", File.ReadAllText(Path.Combine(destination, "readme.txt")));
        Assert.Equal(
            TestUtil.Sha256(payload),
            TestUtil.Sha256File(Path.Combine(destination, "a", "b", "payload.bin")));
    }

    private sealed class SynchronousProgress(Action<ZArchiveExtractionProgress> callback)
        : IProgress<ZArchiveExtractionProgress>
    {
        public void Report(ZArchiveExtractionProgress value) => callback(value);
    }
}
