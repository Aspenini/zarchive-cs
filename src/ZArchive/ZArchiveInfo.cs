using System.Runtime.InteropServices;
using ZArchive.Interop;

namespace ZArchive;

/// <summary>Diagnostic information about the bundled native components.</summary>
public static class ZArchiveInfo
{
    /// <summary>
    /// The version and commit of the upstream ZArchive implementation bundled
    /// in the native bridge, e.g. <c>"ZArchive 0.1.3 (git abc123)"</c>.
    /// </summary>
    public static string UpstreamVersion
    {
        get
        {
            NativeLibraryResolver.EnsureAbiCompatible();
            return Marshal.PtrToStringUTF8(NativeMethods.UpstreamVersion()) ?? "unknown";
        }
    }

    /// <summary>The C ABI version of the loaded native bridge library.</summary>
    public static int NativeAbiVersion
    {
        get
        {
            NativeLibraryResolver.EnsureAbiCompatible();
            return (int)NativeMethods.AbiVersion();
        }
    }
}
