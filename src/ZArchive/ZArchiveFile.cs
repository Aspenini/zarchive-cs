namespace ZArchive;

/// <summary>
/// Convenience helpers for whole-archive operations, mirroring
/// <c>System.IO.Compression.ZipFile</c>.
/// </summary>
public static class ZArchiveFile
{
    /// <summary>
    /// Creates a new archive at <paramref name="destinationArchive"/>
    /// containing the contents of <paramref name="sourceDirectory"/>.
    /// </summary>
    public static void CreateFromDirectory(string sourceDirectory, string destinationArchive)
    {
        using ZArchiveWriter writer = ZArchiveWriter.Create(destinationArchive);
        writer.AddDirectory(sourceDirectory);
        writer.Complete();
    }

    /// <summary>
    /// Extracts the archive at <paramref name="sourceArchive"/> into
    /// <paramref name="destinationDirectory"/>.
    /// </summary>
    public static void ExtractToDirectory(
        string sourceArchive, string destinationDirectory, bool overwrite = false)
    {
        using ZArchiveReader reader = ZArchiveReader.Open(sourceArchive);
        reader.ExtractToDirectory(
            destinationDirectory, new ZArchiveExtractionOptions { Overwrite = overwrite });
    }
}
