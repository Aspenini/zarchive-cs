using System.Runtime.InteropServices;

namespace ZArchive.Interop;

internal static partial class NativeMethods
{
    internal const string LibraryName = "zarchive_dotnet_native";

    static NativeMethods()
    {
        NativeLibraryResolver.Register();
    }

    // ---- Version and diagnostics ----

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_abi_version")]
    internal static partial uint AbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_upstream_version")]
    internal static partial nint UpstreamVersion();

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_get_last_error")]
    internal static partial NativeStatus GetLastError(
        Span<byte> buffer,
        nuint bufferLength,
        out nuint requiredLength);

    // ---- Reader ----

    [LibraryImport(
        LibraryName,
        EntryPoint = "zar_dotnet_reader_open_file",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeStatus ReaderOpenFile(
        string path,
        out NativeReaderHandle reader);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_destroy")]
    internal static partial void ReaderDestroy(nint reader);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_lookup")]
    internal static partial NativeStatus ReaderLookup(
        NativeReaderHandle reader,
        ReadOnlySpan<byte> archivePath,
        nuint archivePathLength,
        int allowFile,
        int allowDirectory,
        out uint node);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_get_entry_type")]
    internal static partial NativeStatus ReaderGetEntryType(
        NativeReaderHandle reader,
        uint node,
        out int isFile,
        out int isDirectory);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_get_file_size")]
    internal static partial NativeStatus ReaderGetFileSize(
        NativeReaderHandle reader,
        uint node,
        out ulong size);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_read_file")]
    internal static partial NativeStatus ReaderReadFile(
        NativeReaderHandle reader,
        uint node,
        ulong offset,
        Span<byte> buffer,
        nuint bufferLength,
        out nuint bytesRead);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_get_directory_count")]
    internal static partial NativeStatus ReaderGetDirectoryCount(
        NativeReaderHandle reader,
        uint node,
        out uint count);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_reader_get_directory_entry")]
    internal static partial NativeStatus ReaderGetDirectoryEntry(
        NativeReaderHandle reader,
        uint directory,
        uint index,
        Span<byte> nameBuffer,
        nuint nameBufferLength,
        out nuint requiredNameLength,
        out int isFile,
        out int isDirectory,
        out ulong size);

    // ---- Writer ----

    [LibraryImport(
        LibraryName,
        EntryPoint = "zar_dotnet_writer_create_file",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeStatus WriterCreateFile(
        string outputPath,
        out NativeWriterHandle writer);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_writer_destroy")]
    internal static partial void WriterDestroy(nint writer);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_writer_make_directory")]
    internal static partial NativeStatus WriterMakeDirectory(
        NativeWriterHandle writer,
        ReadOnlySpan<byte> archivePath,
        nuint archivePathLength,
        int recursive);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_writer_start_file")]
    internal static partial NativeStatus WriterStartFile(
        NativeWriterHandle writer,
        ReadOnlySpan<byte> archivePath,
        nuint archivePathLength);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_writer_append")]
    internal static partial NativeStatus WriterAppend(
        NativeWriterHandle writer,
        ReadOnlySpan<byte> data,
        nuint dataLength);

    [LibraryImport(LibraryName, EntryPoint = "zar_dotnet_writer_finalize")]
    internal static partial NativeStatus WriterFinalize(
        NativeWriterHandle writer);
}
