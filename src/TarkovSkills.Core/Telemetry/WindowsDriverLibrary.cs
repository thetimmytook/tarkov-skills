using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

internal interface IWindowsDriverLibrary : IDisposable
{
    nint Handle { get; }
    nint Export(string name);
}

internal sealed class WindowsDriverLibrary(string name) : IWindowsDriverLibrary
{
    public nint Handle { get; private set; } = LoadLibraryExW(name, 0, 0x800); // LOAD_LIBRARY_SEARCH_SYSTEM32
    public nint Export(string export) => Handle == 0 ? 0 : GetProcAddress(Handle, export);
    public void Dispose()
    {
        if (Handle == 0) return;
        FreeLibrary(Handle); Handle = 0;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint LoadLibraryExW(string name, nint reserved, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern nint GetProcAddress(nint handle, string name);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern int FreeLibrary(nint handle);
}

[StructLayout(LayoutKind.Sequential)]
internal struct GpuLuid
{
    public uint Low;
    public int High;
    internal readonly string Key => GpuCounters.Key(unchecked((uint)High), Low);
}
