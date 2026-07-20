namespace ZArchive;

/// <summary>
/// Thrown when a ZArchive native operation fails in a way that does not map
/// to a more specific .NET exception type.
/// </summary>
public class ZArchiveException : Exception
{
    /// <summary>The raw status code reported by the native bridge.</summary>
    public int NativeErrorCode { get; }

    /// <summary>Initializes a new instance of the exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="nativeErrorCode">The native bridge status code.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ZArchiveException(
        string message,
        int nativeErrorCode = 0,
        Exception? innerException = null)
        : base(message, innerException)
    {
        NativeErrorCode = nativeErrorCode;
    }
}
