using System.Diagnostics.CodeAnalysis;
using System.Text;
using ZArchive.Interop;

namespace ZArchive;

/// <summary>
/// Read-only view over a ZArchive (.zar / .wua) file. Instances own a native
/// reader and must be disposed. Concurrent reads from one reader are
/// supported; disposal must not race with active reads.
/// </summary>
public sealed class ZArchiveReader : IDisposable
{
    private readonly NativeReaderHandle _handle;
    private readonly string _archiveFilePath;
    private ZArchiveEntry? _root;
    private volatile bool _disposed;

    private ZArchiveReader(NativeReaderHandle handle, string archiveFilePath)
    {
        _handle = handle;
        _archiveFilePath = archiveFilePath;
    }

    /// <summary>Opens an archive file for reading.</summary>
    /// <param name="archivePath">Path of the archive on the host filesystem.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not a valid ZArchive.</exception>
    public static ZArchiveReader Open(string archivePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        NativeLibraryResolver.EnsureAbiCompatible();
        NativeStatus status = NativeMethods.ReaderOpenFile(archivePath, out NativeReaderHandle handle);
        if (status != NativeStatus.Ok)
        {
            handle.Dispose();
            throw NativeError.CreateException(status, "Could not open archive", archivePath);
        }
        return new ZArchiveReader(handle, archivePath);
    }

    /// <summary>The root directory entry of the archive.</summary>
    public ZArchiveEntry Root
    {
        get
        {
            ThrowIfDisposed();
            return _root ??= GetEntry(string.Empty)
                ?? throw new InvalidDataException("Archive has no root directory.");
        }
    }

    /// <summary>
    /// Returns the entry at <paramref name="path"/>, or <see langword="null"/>
    /// when no file or directory exists at that path.
    /// </summary>
    public ZArchiveEntry? GetEntry(string path)
    {
        string normalized = ArchivePath.Normalize(path, nameof(path));
        NativeStatus status = Lookup(normalized, allowFile: true, allowDirectory: true, out uint node);
        if (status == NativeStatus.NotFound)
            return null;
        NativeError.ThrowIfFailed(status, "Could not look up archive entry", normalized);
        return CreateEntry(node, normalized);
    }

    /// <summary>Attempts to find the entry at <paramref name="path"/>.</summary>
    public bool TryGetEntry(string path, [NotNullWhen(true)] out ZArchiveEntry? entry)
    {
        entry = GetEntry(path);
        return entry is not null;
    }

    /// <summary>Returns whether a file exists at <paramref name="path"/>.</summary>
    public bool FileExists(string path)
    {
        string normalized = ArchivePath.Normalize(path, nameof(path));
        return Lookup(normalized, allowFile: true, allowDirectory: false, out _) == NativeStatus.Ok;
    }

    /// <summary>Returns whether a directory exists at <paramref name="path"/>.</summary>
    public bool DirectoryExists(string path)
    {
        string normalized = ArchivePath.Normalize(path, nameof(path));
        return Lookup(normalized, allowFile: false, allowDirectory: true, out _) == NativeStatus.Ok;
    }

    /// <summary>
    /// Enumerates the entries of the directory at
    /// <paramref name="directoryPath"/> (the archive root by default).
    /// </summary>
    /// <param name="directoryPath">Archive path of the directory to enumerate.</param>
    /// <param name="recursive">Whether to descend into subdirectories.</param>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public IEnumerable<ZArchiveEntry> EnumerateEntries(
        string directoryPath = "", bool recursive = false)
    {
        string normalized = ArchivePath.Normalize(directoryPath, nameof(directoryPath));
        NativeStatus status = Lookup(normalized, allowFile: false, allowDirectory: true, out uint node);
        if (status == NativeStatus.NotFound)
            throw new DirectoryNotFoundException(
                $"The archive does not contain a directory at '{normalized}'.");
        NativeError.ThrowIfFailed(status, "Could not look up archive directory", normalized);
        return EnumerateCore(node, normalized, recursive);
    }

    private IEnumerable<ZArchiveEntry> EnumerateCore(uint directoryNode, string directoryPath, bool recursive)
    {
        uint count = GetDirectoryEntryCount(directoryNode, directoryPath);
        for (uint index = 0; index < count; index++)
        {
            ZArchiveEntry entry = GetDirectoryEntryAt(directoryNode, directoryPath, index);
            yield return entry;
            if (recursive && entry.IsDirectory)
            {
                NativeStatus status = Lookup(
                    entry.FullName, allowFile: false, allowDirectory: true, out uint childNode);
                NativeError.ThrowIfFailed(status, "Could not look up archive directory", entry.FullName);
                foreach (ZArchiveEntry nested in EnumerateCore(childNode, entry.FullName, recursive: true))
                    yield return nested;
            }
        }
    }

    /// <summary>Opens the file at <paramref name="path"/> as a read-only seekable stream.</summary>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    public Stream OpenRead(string path)
    {
        string normalized = ArchivePath.Normalize(path, nameof(path));
        NativeStatus status = Lookup(normalized, allowFile: true, allowDirectory: false, out uint node);
        if (status == NativeStatus.NotFound && DirectoryExists(normalized))
            throw new UnauthorizedAccessException(
                $"'{normalized}' is a directory, not a file.");
        NativeError.ThrowIfFailed(status, "Could not open archive file", normalized);
        return new ZArchiveEntryStream(this, node, GetFileLength(node, normalized), normalized);
    }

    /// <summary>Extracts a single archived file to the host filesystem.</summary>
    /// <param name="archivePath">Path of the file inside the archive.</param>
    /// <param name="destinationPath">Host filesystem path to write.</param>
    /// <param name="overwrite">Whether an existing destination file may be replaced.</param>
    public void ExtractFile(string archivePath, string destinationPath, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);
        using Stream source = OpenRead(archivePath);
        using FileStream destination = new(
            destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
    }

    /// <summary>
    /// Extracts the complete archive into
    /// <paramref name="destinationDirectory"/>, creating it if necessary.
    /// Entries whose paths would escape the destination directory are
    /// rejected with an <see cref="IOException"/>.
    /// </summary>
    public void ExtractToDirectory(
        string destinationDirectory, ZArchiveExtractionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);
        options ??= new ZArchiveExtractionOptions();
        Directory.CreateDirectory(destinationDirectory);

        long entriesProcessed = 0;
        byte[] buffer = new byte[256 * 1024];
        foreach (ZArchiveEntry entry in EnumerateEntries(recursive: true))
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            string destination = ArchivePath.GetSafeExtractionPath(destinationDirectory, entry.FullName);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destination);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using Stream source = entry.OpenRead();
                using FileStream target = new(
                    destination, options.Overwrite ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    target.Write(buffer, 0, read);
                }
            }
            entriesProcessed++;
            options.Progress?.Report(new ZArchiveExtractionProgress(entriesProcessed, entry.FullName));
        }
    }

    /// <summary>Releases the native reader. Open entry streams become unusable.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _handle.Dispose();
    }

    // ---- Internal plumbing shared with ZArchiveEntry / ZArchiveEntryStream ----

    internal ZArchiveEntry CreateEntry(uint node, string normalizedFullName)
    {
        NativeStatus status = NativeMethods.ReaderGetEntryType(
            _handle, node, out int isFile, out int isDirectory);
        NativeError.ThrowIfFailed(status, "Could not query archive entry type", normalizedFullName);
        _ = isDirectory;
        long length = isFile != 0 ? GetFileLength(node, normalizedFullName) : 0;
        return new ZArchiveEntry(
            this,
            normalizedFullName,
            isFile != 0 ? ZArchiveEntryType.File : ZArchiveEntryType.Directory,
            length);
    }

    internal Stream OpenReadByEntry(ZArchiveEntry entry)
    {
        return OpenRead(entry.FullName);
    }

    internal int ReadFileAt(uint node, ulong offset, Span<byte> buffer, string archivePath)
    {
        ThrowIfDisposed();
        NativeStatus status = NativeMethods.ReaderReadFile(
            _handle, node, offset, buffer, (nuint)buffer.Length, out nuint bytesRead);
        NativeError.ThrowIfFailed(status, "Could not read archive file", archivePath);
        return checked((int)bytesRead);
    }

    internal void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private NativeStatus Lookup(string normalizedPath, bool allowFile, bool allowDirectory, out uint node)
    {
        ThrowIfDisposed();
        byte[] utf8 = Encoding.UTF8.GetBytes(normalizedPath);
        NativeStatus status = NativeMethods.ReaderLookup(
            _handle, utf8, (nuint)utf8.Length, allowFile ? 1 : 0, allowDirectory ? 1 : 0, out node);
        if (status != NativeStatus.Ok || (allowFile && allowDirectory))
            return status;

        // Upstream LookUp does not honor its allow-file/allow-directory
        // parameters, so the type filter is enforced here.
        NativeStatus typeStatus = NativeMethods.ReaderGetEntryType(
            _handle, node, out int isFile, out _);
        if (typeStatus != NativeStatus.Ok)
            return typeStatus;
        if ((isFile != 0 && !allowFile) || (isFile == 0 && !allowDirectory))
        {
            node = uint.MaxValue;
            return NativeStatus.NotFound;
        }
        return NativeStatus.Ok;
    }

    private long GetFileLength(uint node, string normalizedPath)
    {
        NativeStatus status = NativeMethods.ReaderGetFileSize(_handle, node, out ulong size);
        NativeError.ThrowIfFailed(status, "Could not query archive file size", normalizedPath);
        if (size > long.MaxValue)
            throw new InvalidDataException(
                $"Archive file '{normalizedPath}' reports a size larger than long.MaxValue.");
        return (long)size;
    }

    private uint GetDirectoryEntryCount(uint directoryNode, string directoryPath)
    {
        ThrowIfDisposed();
        NativeStatus status = NativeMethods.ReaderGetDirectoryCount(_handle, directoryNode, out uint count);
        NativeError.ThrowIfFailed(status, "Could not enumerate archive directory", directoryPath);
        return count;
    }

    private ZArchiveEntry GetDirectoryEntryAt(uint directoryNode, string directoryPath, uint index)
    {
        ThrowIfDisposed();
        Span<byte> nameBuffer = stackalloc byte[256];
        NativeStatus status = NativeMethods.ReaderGetDirectoryEntry(
            _handle, directoryNode, index, nameBuffer, (nuint)nameBuffer.Length,
            out nuint requiredLength, out int isFile, out int isDirectory, out ulong size);
        byte[]? largeBuffer = null;
        if (status == NativeStatus.BufferTooSmall)
        {
            largeBuffer = new byte[checked((int)requiredLength)];
            status = NativeMethods.ReaderGetDirectoryEntry(
                _handle, directoryNode, index, largeBuffer, (nuint)largeBuffer.Length,
                out requiredLength, out isFile, out isDirectory, out size);
        }
        NativeError.ThrowIfFailed(status, "Could not enumerate archive directory", directoryPath);

        ReadOnlySpan<byte> nameBytes = largeBuffer is null ? nameBuffer : largeBuffer;
        nameBytes = nameBytes[..(checked((int)requiredLength) - 1)]; // strip NUL
        string name = Encoding.UTF8.GetString(nameBytes);
        _ = isDirectory;
        long length = isFile != 0 ? checked((long)size) : 0;
        return new ZArchiveEntry(
            this,
            ArchivePath.Combine(directoryPath, name),
            isFile != 0 ? ZArchiveEntryType.File : ZArchiveEntryType.Directory,
            length);
    }
}
