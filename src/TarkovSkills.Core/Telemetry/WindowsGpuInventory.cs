using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

internal static class WindowsGpuInventory
{
    public static IReadOnlyList<AdapterReading> Read()
    {
        var id = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
        if (CreateDXGIFactory1(ref id, out var factory) < 0) return [];
        var adapters = new List<AdapterReading>();
        try
        {
            var enumerate = Method<EnumAdapters1>(factory, 12);
            for (uint index = 0; index < 32; index++)
            {
                if (enumerate(factory, index, out var adapter) < 0) break;
                try
                {
                    if (Method<GetDesc1>(adapter, 10)(adapter, out var desc) < 0 || (desc.Flags & 2) != 0) continue;
                    adapters.Add(new(GpuCounters.Key(unchecked((uint)desc.Luid.High), desc.Luid.Low), desc.Description,
                        desc.DedicatedVideoMemory.ToUInt64(), ReadUnifiedMemory(adapter), desc.Vendor));
                }
                finally { Marshal.Release(adapter); }
            }
        }
        finally { Marshal.Release(factory); }
        return adapters;
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    private static bool? ReadUnifiedMemory(IntPtr adapter)
    {
        // UMA classification prevents firmware-reserved system RAM being advertised
        // as physical VRAM. This device is used only for a read-only feature query.
        try
        {
            var id = new Guid("189819f1-1db6-4b57-be54-1821339b85f7"); // ID3D12Device
            if (D3D12CreateDevice(adapter, 0xB000, ref id, out var device) < 0) return null;
            try
            {
                var architecture = new Architecture();
                return Method<CheckFeatureSupport>(device, 13)(device, 1, ref architecture, (uint)Marshal.SizeOf<Architecture>()) >= 0
                    ? architecture.Unified != 0 : null;
            }
            finally { Marshal.Release(device); }
        }
        catch { return null; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Architecture { public uint Node; public int TileBased, Unified, CacheCoherent; }
    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Vendor, Device, Subsystem, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public Luid Luid;
        public uint Flags;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumAdapters1(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetDesc1(IntPtr adapter, out AdapterDescription desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CheckFeatureSupport(IntPtr device, uint feature, ref Architecture data, uint size);
    [DllImport("dxgi.dll", ExactSpelling = true)] private static extern int CreateDXGIFactory1(ref Guid id, out IntPtr factory);
    [DllImport("d3d12.dll", ExactSpelling = true)] private static extern int D3D12CreateDevice(IntPtr adapter, uint minimumFeatureLevel, ref Guid id, out IntPtr device);
}
