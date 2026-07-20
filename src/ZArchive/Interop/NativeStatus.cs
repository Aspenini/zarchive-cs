namespace ZArchive.Interop;

/// <summary>Status codes returned by the native bridge (zar_dotnet_status).</summary>
internal enum NativeStatus
{
    Ok = 0,
    InvalidArgument = 1,
    NotFound = 2,
    InvalidNode = 3,
    NotAFile = 4,
    NotADirectory = 5,
    IoError = 6,
    InvalidArchive = 7,
    BufferTooSmall = 8,
    InvalidState = 9,
    OutOfMemory = 10,
    InternalError = 255,
}
