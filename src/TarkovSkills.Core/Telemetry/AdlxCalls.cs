using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

// Windows C interface ABI. IADLXSystem has its own table and is NOT IADLXInterface.
internal static class AdlxCalls
{
    internal static T Method<T>(nint instance, int slot) where T : Delegate
    {
        if (instance == 0) throw new InvalidOperationException();
        var address = Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size);
        return address == 0 ? throw new InvalidOperationException() : Marshal.GetDelegateForFunctionPointer<T>(address);
    }
    internal static void Release(nint instance) { if (instance != 0) Method<Reference>(instance, 1)(instance); }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FullVersion(out ulong version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Initialize(ulong version, out nint system);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Terminate();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Reference(nint instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Object(nint instance, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Query(nint instance,
        [MarshalAs(UnmanagedType.LPWStr)] string id, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate uint Count(nint instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int At(nint instance, uint index, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Luid(nint instance, out GpuLuid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Support(nint instance, nint gpu, out nint caps);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Boolean(nint instance, out byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Integer(nint instance, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int History(nint instance, nint gpu,
        int startMs, int endMs, out nint list);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Timestamp(nint instance, out long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int Usage(nint instance, out double value);
}
