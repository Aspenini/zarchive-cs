using System.Text;

namespace ZArchive.Interop;

/// <summary>Translates native status codes into .NET exceptions.</summary>
internal static class NativeError
{
    /// <summary>Retrieves the calling thread's last native error message.</summary>
    internal static string GetLastErrorMessage()
    {
        Span<byte> buffer = stackalloc byte[512];
        NativeStatus status = NativeMethods.GetLastError(
            buffer, (nuint)buffer.Length, out nuint required);
        if (status == NativeStatus.Ok)
            return DecodeNulTerminated(buffer);
        if (status == NativeStatus.BufferTooSmall && required > 0 && required < 1024 * 1024)
        {
            byte[] large = new byte[(int)required];
            status = NativeMethods.GetLastError(large, (nuint)large.Length, out _);
            if (status == NativeStatus.Ok)
                return DecodeNulTerminated(large);
        }
        return string.Empty;
    }

    /// <summary>
    /// Throws the .NET exception mapped to <paramref name="status"/>. Must only
    /// be called for non-<see cref="NativeStatus.Ok"/> statuses.
    /// </summary>
    internal static Exception CreateException(NativeStatus status, string operation, string? path = null)
    {
        string detail = GetLastErrorMessage();
        string message = detail.Length != 0
            ? $"{operation}: {detail}"
            : $"{operation} failed with native status {status}.";
        if (path is not null)
            message += $" (path: '{path}')";

        return status switch
        {
            NativeStatus.InvalidArgument => new ArgumentException(message),
            NativeStatus.NotFound => new FileNotFoundException(message, path),
            NativeStatus.NotAFile => new InvalidOperationException(message),
            NativeStatus.NotADirectory => new InvalidOperationException(message),
            NativeStatus.IoError => new IOException(message),
            NativeStatus.InvalidArchive => new InvalidDataException(message),
            NativeStatus.InvalidState => new InvalidOperationException(message),
            NativeStatus.OutOfMemory => new OutOfMemoryException(message),
            _ => new ZArchiveException(message, (int)status),
        };
    }

    internal static void ThrowIfFailed(NativeStatus status, string operation, string? path = null)
    {
        if (status != NativeStatus.Ok)
            throw CreateException(status, operation, path);
    }

    private static string DecodeNulTerminated(ReadOnlySpan<byte> buffer)
    {
        int terminator = buffer.IndexOf((byte)0);
        if (terminator >= 0)
            buffer = buffer[..terminator];
        return Encoding.UTF8.GetString(buffer);
    }
}
