using System.Security.Cryptography;

namespace ZArchive.Tests;

/// <summary>Per-test temporary directory that is deleted on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "zarchive-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}

public static class TestUtil
{
    public static byte[] DeterministicBytes(int length, int seed = 1234)
    {
        var random = new Random(seed);
        byte[] data = new byte[length];
        random.NextBytes(data);
        return data;
    }

    public static string Sha256(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data));

    public static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Creates an archive with a small representative entry tree.</summary>
    public static Dictionary<string, byte[]> CreateSampleArchive(string archivePath)
    {
        var files = new Dictionary<string, byte[]>
        {
            ["hello.txt"] = "Hello, ZArchive!"u8.ToArray(),
            ["empty.bin"] = Array.Empty<byte>(),
            ["data/block-exact.bin"] = DeterministicBytes(64 * 1024, seed: 1),
            ["data/block-minus-one.bin"] = DeterministicBytes(64 * 1024 - 1, seed: 2),
            ["data/block-plus-one.bin"] = DeterministicBytes(64 * 1024 + 1, seed: 3),
            ["data/nested/large.bin"] = DeterministicBytes(1_000_000, seed: 4),
            ["Mixed Case & punct'd.txt"] = DeterministicBytes(100, seed: 5),
        };
        using ZArchiveWriter writer = ZArchiveWriter.Create(archivePath);
        writer.CreateDirectory("emptydir");
        foreach ((string path, byte[] content) in files)
        {
            using Stream entry = writer.CreateEntry(path);
            entry.Write(content);
        }
        writer.Complete();
        return files;
    }
}
