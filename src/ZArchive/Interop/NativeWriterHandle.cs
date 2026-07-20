using System.Runtime.InteropServices;

namespace ZArchive.Interop;

internal sealed class NativeWriterHandle : SafeHandle
{
    public NativeWriterHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.WriterDestroy(handle);
        return true;
    }
}
