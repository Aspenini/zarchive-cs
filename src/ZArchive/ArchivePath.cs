namespace ZArchive;

/// <summary>
/// Normalization and safety helpers for paths inside an archive. Archive
/// paths use '/' as the canonical separator; '\' is accepted on input and
/// normalized. A leading '/' is optional and removed.
/// </summary>
internal static class ArchivePath
{
    /// <summary>Normalizes an archive path for lookup or creation.</summary>
    internal static string Normalize(string path, string paramName)
    {
        ArgumentNullException.ThrowIfNull(path, paramName);
        if (path.Contains('\0'))
            throw new ArgumentException("Archive paths must not contain NUL characters.", paramName);
        string normalized = path.Replace('\\', '/').TrimStart('/');
        return normalized;
    }

    /// <summary>Returns the last segment of a normalized archive path.</summary>
    internal static string GetName(string normalizedPath)
    {
        int lastSlash = normalizedPath.TrimEnd('/').LastIndexOf('/');
        string trimmed = normalizedPath.TrimEnd('/');
        return lastSlash < 0 ? trimmed : trimmed[(lastSlash + 1)..];
    }

    /// <summary>Combines two normalized archive path fragments.</summary>
    internal static string Combine(string left, string right)
    {
        if (left.Length == 0)
            return right;
        if (right.Length == 0)
            return left;
        return left.TrimEnd('/') + "/" + right;
    }

    /// <summary>
    /// Resolves an archive-relative path against a destination root and
    /// verifies the result stays inside that root. Rejects rooted paths,
    /// traversal segments and names invalid on the local filesystem.
    /// </summary>
    internal static string GetSafeExtractionPath(string destinationRoot, string relativeArchivePath)
    {
        string fullRoot = Path.GetFullPath(destinationRoot);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            fullRoot += Path.DirectorySeparatorChar;

        if (relativeArchivePath.Length == 0)
            throw new IOException("Archive entry has an empty path.");
        if (Path.IsPathRooted(relativeArchivePath))
            throw new IOException(
                $"Archive entry '{relativeArchivePath}' has a rooted path and cannot be extracted safely.");

        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (string segment in relativeArchivePath.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
                throw new IOException(
                    $"Archive entry '{relativeArchivePath}' contains an unsafe path segment.");
            if (segment.IndexOfAny(invalidChars) >= 0)
                throw new IOException(
                    $"Archive entry '{relativeArchivePath}' contains characters that are invalid " +
                    "in file names on this platform.");
        }

        string candidate = Path.GetFullPath(Path.Combine(
            fullRoot, relativeArchivePath.Replace('/', Path.DirectorySeparatorChar)));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(fullRoot, comparison))
            throw new IOException(
                $"Archive entry '{relativeArchivePath}' would be extracted outside the destination directory.");
        return candidate;
    }
}
