using System.Runtime.InteropServices;

namespace ZArchive.Interop;

internal sealed class NativeReaderHandle : SafeHandle
{
    public NativeReaderHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.ReaderDestroy(handle);
        return true;
    }
}
