using System.Reflection;
using System.Runtime.InteropServices;

namespace ZArchive.Interop;

/// <summary>
/// Registers a <see cref="NativeLibrary"/> resolver that supplements the
/// default .NET native asset probing, and validates the bridge ABI version
/// before any handle is created.
/// </summary>
internal static class NativeLibraryResolver
{
    private static bool s_registered;
    private static bool s_abiValidated;
    private static readonly object s_lock = new();

    internal static void Register()
    {
        lock (s_lock)
        {
            if (s_registered)
                return;
            NativeLibrary.SetDllImportResolver(
                typeof(NativeLibraryResolver).Assembly, Resolve);
            s_registered = true;
        }
    }

    /// <summary>
    /// Validates the native ABI version once per process. Called before the
    /// first reader or writer handle is created.
    /// </summary>
    internal static void EnsureAbiCompatible()
    {
        if (s_abiValidated)
            return;
        lock (s_lock)
        {
            if (s_abiValidated)
                return;
            uint abiVersion;
            try
            {
                abiVersion = NativeMethods.AbiVersion();
            }
            catch (DllNotFoundException ex)
            {
                throw new DllNotFoundException(BuildLoadFailureMessage(), ex);
            }
            if (abiVersion != 1)
            {
                throw new ZArchiveException(
                    $"The native library '{NativeMethods.LibraryName}' reports ABI version {abiVersion}, " +
                    "but this version of ZArchive.NET requires ABI version 1. " +
                    "The managed and native components are mismatched.",
                    nativeErrorCode: (int)NativeStatus.InternalError);
            }
            s_abiValidated = true;
        }
    }

    private static nint Resolve(
        string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != NativeMethods.LibraryName)
            return nint.Zero;

        // Default probing (handles NuGet runtimes/<rid>/native and the
        // application directory in published apps).
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out nint handle))
            return handle;

        // Fallback probing for development layouts and test runners.
        string fileName = GetPlatformFileName(libraryName);
        foreach (string directory in GetProbingDirectories())
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
                return handle;
        }
        return nint.Zero;
    }

    private static IEnumerable<string> GetProbingDirectories()
    {
        string rid = RuntimeInformation.RuntimeIdentifier;
        string baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return Path.Combine(baseDirectory, "runtimes", rid, "native");

        string? assemblyDirectory = Path.GetDirectoryName(
            typeof(NativeLibraryResolver).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDirectory) &&
            !string.Equals(assemblyDirectory, baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return assemblyDirectory;
            yield return Path.Combine(assemblyDirectory, "runtimes", rid, "native");
        }
    }

    private static string GetPlatformFileName(string libraryName)
    {
        if (OperatingSystem.IsWindows())
            return libraryName + ".dll";
        if (OperatingSystem.IsMacOS())
            return "lib" + libraryName + ".dylib";
        return "lib" + libraryName + ".so";
    }

    private static string BuildLoadFailureMessage()
    {
        return
            $"Could not load the ZArchive native library '{GetPlatformFileName(NativeMethods.LibraryName)}'. " +
            $"OS: {RuntimeInformation.OSDescription}; " +
            $"process architecture: {RuntimeInformation.ProcessArchitecture}; " +
            $"runtime identifier: {RuntimeInformation.RuntimeIdentifier}. " +
            "Ensure the ZArchive.NET package's native asset for this runtime identifier is present " +
            "(runtimes/<rid>/native inside the package). If this platform is not listed in the " +
            "package's supported runtime identifiers, it is not yet supported.";
    }
}
