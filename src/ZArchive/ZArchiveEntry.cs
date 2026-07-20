namespace ZArchive;

/// <summary>
/// A file or directory inside a ZArchive. Entries are lightweight snapshots;
/// they remain valid only while their owning <see cref="ZArchiveReader"/> is
/// not disposed.
/// </summary>
public sealed class ZArchiveEntry
{
    private readonly ZArchiveReader _reader;

    internal ZArchiveEntry(
        ZArchiveReader reader, string fullName, ZArchiveEntryType entryType, long length)
    {
        _reader = reader;
        FullName = fullName;
        EntryType = entryType;
        Length = length;
    }

    /// <summary>The final path segment of the entry (empty for the root).</summary>
    public string Name => ArchivePath.GetName(FullName);

    /// <summary>The full archive path of the entry, using '/' separators.</summary>
    public string FullName { get; }

    /// <summary>Whether the entry is a file or a directory.</summary>
    public ZArchiveEntryType EntryType { get; }

    /// <summary>Whether the entry is a file.</summary>
    public bool IsFile => EntryType == ZArchiveEntryType.File;

    /// <summary>Whether the entry is a directory.</summary>
    public bool IsDirectory => EntryType == ZArchiveEntryType.Directory;

    /// <summary>The uncompressed size in bytes. Zero for directories.</summary>
    public long Length { get; }

    /// <summary>Opens the file entry as a read-only seekable stream.</summary>
    /// <exception cref="UnauthorizedAccessException">The entry is a directory.</exception>
    public Stream OpenRead()
    {
        return _reader.OpenReadByEntry(this);
    }

    /// <inheritdoc />
    public override string ToString() => $"{EntryType}: {FullName}";
}
