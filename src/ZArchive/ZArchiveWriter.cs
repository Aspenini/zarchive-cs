using System.Text;
using ZArchive.Interop;

namespace ZArchive;

/// <summary>
/// Creates a new ZArchive file. Archives are append-only: entries are added
/// one at a time, and the archive becomes valid only after
/// <see cref="Complete"/> succeeds. Disposing a writer that was never
/// completed deletes the partial output file.
/// </summary>
public sealed class ZArchiveWriter : IDisposable
{
    private readonly NativeWriterHandle _handle;
    private ZArchiveWriterEntryStream? _activeEntry;
    private bool _completed;
    private bool _disposed;

    private ZArchiveWriter(NativeWriterHandle handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a new archive at <paramref name="archivePath"/>.</summary>
    /// <param name="archivePath">Host filesystem path of the archive to create.</param>
    /// <exception cref="IOException">The output file could not be created.</exception>
    public static ZArchiveWriter Create(string archivePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        NativeLibraryResolver.EnsureAbiCompatible();
        NativeStatus status = NativeMethods.WriterCreateFile(archivePath, out NativeWriterHandle handle);
        if (status != NativeStatus.Ok)
        {
            handle.Dispose();
            throw NativeError.CreateException(status, "Could not create archive", archivePath);
        }
        return new ZArchiveWriter(handle);
    }

    /// <summary>Creates a directory inside the archive.</summary>
    /// <param name="path">Archive path of the directory.</param>
    /// <param name="recursive">
    /// When <see langword="true"/> (the default), missing parent directories
    /// are created and existing directories are tolerated. When
    /// <see langword="false"/>, the parent must exist and the directory must
    /// not already exist.
    /// </param>
    public void CreateDirectory(string path, bool recursive = true)
    {
        ThrowIfNotWritable();
        string normalized = ArchivePath.Normalize(path, nameof(path));
        if (normalized.Length == 0)
            return;
        MakeDirectoryCore(normalized, recursive);
    }

    /// <summary>
    /// Starts a new file entry and returns a write-only stream for its
    /// content. Missing parent directories are created automatically. Only
    /// one entry stream may be open at a time; dispose it before starting
    /// the next entry or calling <see cref="Complete"/>.
    /// </summary>
    /// <param name="path">Archive path of the new file.</param>
    public Stream CreateEntry(string path)
    {
        ThrowIfNotWritable();
        string normalized = ArchivePath.Normalize(path, nameof(path));
        if (normalized.Length == 0 || normalized.EndsWith('/'))
            throw new ArgumentException("A file entry requires a non-empty file name.", nameof(path));

        int lastSlash = normalized.LastIndexOf('/');
        if (lastSlash > 0)
            MakeDirectoryCore(normalized[..lastSlash], recursive: true);

        byte[] utf8 = Encoding.UTF8.GetBytes(normalized);
        NativeStatus status = NativeMethods.WriterStartFile(_handle, utf8, (nuint)utf8.Length);
        NativeError.ThrowIfFailed(status, "Could not start archive entry", normalized);
        _activeEntry = new ZArchiveWriterEntryStream(this);
        return _activeEntry;
    }

    /// <summary>Adds a file from the host filesystem to the archive.</summary>
    /// <param name="sourcePath">Host path of the file to read.</param>
    /// <param name="archivePath">Archive path of the new entry.</param>
    public void AddFile(string sourcePath, string archivePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        using FileStream source = File.OpenRead(sourcePath);
        using Stream entry = CreateEntry(archivePath);
        source.CopyTo(entry);
    }

    /// <summary>
    /// Adds the contents of a host directory to the archive under
    /// <paramref name="archiveRoot"/> ("" for the archive root).
    /// </summary>
    /// <param name="sourceDirectory">Host directory to read.</param>
    /// <param name="archiveRoot">Archive directory receiving the contents.</param>
    /// <param name="recursive">Whether to include subdirectories.</param>
    public void AddDirectory(string sourceDirectory, string archiveRoot = "", bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);
        ThrowIfNotWritable();
        string root = ArchivePath.Normalize(archiveRoot, nameof(archiveRoot)).TrimEnd('/');
        string fullSource = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(fullSource))
            throw new DirectoryNotFoundException($"Source directory '{sourceDirectory}' does not exist.");
        if (root.Length != 0)
            CreateDirectory(root);

        SearchOption searchOption = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        if (recursive)
        {
            foreach (string directory in Directory.EnumerateDirectories(fullSource, "*", searchOption))
                CreateDirectory(ArchivePath.Combine(root, ToArchiveRelativePath(fullSource, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(fullSource, "*", searchOption))
            AddFile(file, ArchivePath.Combine(root, ToArchiveRelativePath(fullSource, file)));
    }

    /// <summary>
    /// Writes the archive footer and marks the archive as valid. May only be
    /// called once, and not while an entry stream is open.
    /// </summary>
    public void Complete()
    {
        ThrowIfNotWritable();
        NativeStatus status = NativeMethods.WriterFinalize(_handle);
        NativeError.ThrowIfFailed(status, "Could not finalize archive");
        _completed = true;
    }

    /// <summary>
    /// Releases the native writer. If <see cref="Complete"/> was never
    /// called, the partial output file is deleted.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _activeEntry = null;
        _handle.Dispose();
    }

    // ---- Internal plumbing for ZArchiveWriterEntryStream ----

    internal void AppendToActiveEntry(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeStatus status = NativeMethods.WriterAppend(_handle, data, (nuint)data.Length);
        NativeError.ThrowIfFailed(status, "Could not write archive entry data");
    }

    internal void OnEntryStreamClosed(ZArchiveWriterEntryStream stream)
    {
        if (ReferenceEquals(_activeEntry, stream))
            _activeEntry = null;
    }

    private void MakeDirectoryCore(string normalizedPath, bool recursive)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(normalizedPath);
        NativeStatus status = NativeMethods.WriterMakeDirectory(
            _handle, utf8, (nuint)utf8.Length, recursive ? 1 : 0);
        NativeError.ThrowIfFailed(status, "Could not create archive directory", normalizedPath);
    }

    private void ThrowIfNotWritable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
            throw new InvalidOperationException("The archive has already been completed.");
        if (_activeEntry is not null)
            throw new InvalidOperationException(
                "An entry stream is still open. Dispose it before continuing.");
    }

    private static string ToArchiveRelativePath(string root, string fullPath)
    {
        return Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
