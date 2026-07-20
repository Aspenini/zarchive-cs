namespace ZArchive;

/// <summary>Options for <see cref="ZArchiveReader.ExtractToDirectory"/>.</summary>
public sealed class ZArchiveExtractionOptions
{
    /// <summary>
    /// Whether existing destination files may be overwritten. When
    /// <see langword="false"/> (the default), extraction fails with an
    /// <see cref="IOException"/> if a destination file already exists.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>Optional progress sink, reported once per extracted entry.</summary>
    public IProgress<ZArchiveExtractionProgress>? Progress { get; set; }

    /// <summary>Token checked between chunks and entries during extraction.</summary>
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>Progress snapshot reported during extraction.</summary>
/// <param name="EntriesProcessed">Number of entries fully processed so far.</param>
/// <param name="CurrentEntryPath">Archive path of the most recently processed entry.</param>
public readonly record struct ZArchiveExtractionProgress(
    long EntriesProcessed, string CurrentEntryPath);
