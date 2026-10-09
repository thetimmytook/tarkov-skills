using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

// Minimal installed-driver binding; no settings, clocks, sensors or process queries.
// ABI checked against NVIDIA/nvapi commit 70d337db9186e968eab622f7e786de7e437faf3d.
internal sealed class NvApiGpuUsage : IGpuUsageProvider
{
    private readonly IWindowsDriverLibrary library;
    private readonly Func<double> clock;
    private readonly Dictionary<string, nint> handles = [];
    private Simple? unload;
    private DynamicStates? read;
    private bool initialized, disposed;
    public string Source => GpuUsageSources.Nvidia;

    internal NvApiGpuUsage(IReadOnlyList<AdapterReading> adapters, IWindowsDriverLibrary? native = null, Func<double>? clock = null)
    {
        library = native ?? new WindowsDriverLibrary("nvapi64.dll");
        this.clock = clock ?? ResourceSampler.Clock;
        bool ready = false;
        try
        {
            var address = library.Export("nvapi_QueryInterface");
            if (address == 0) return;
            var query = Marshal.GetDelegateForFunctionPointer<QueryInterface>(address);
            T Function<T>(uint id) where T : Delegate
            {
                var pointer = query(id);
                return pointer == 0 ? throw new InvalidOperationException() : Marshal.GetDelegateForFunctionPointer<T>(pointer);
            }
            unload = Function<Simple>(0xd22bdd7e);
            if (Function<Simple>(0x0150e828)() != 0) return;
            initialized = true;
            var physical = new nint[64];
            if (Function<Enumerate>(0xe5ac921f)(physical, out uint count) != 0 || count is 0 or > 64) return;
            var logical = Function<LogicalHandle>(0xadd604d1);
            var info = Function<LogicalInfo>(0x842b066e);
            read = Function<DynamicStates>(0x60ded2ed);
            var candidates = new List<(string Key, nint Handle)>();
            var luidBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<GpuLuid>());
            try
            {
                foreach (var handle in physical.Take((int)count).Where(h => h != 0))
                {
                    if (logical(handle, out var logicalGpu) != 0 || logicalGpu == 0) continue;
                    Marshal.StructureToPtr(new GpuLuid(), luidBuffer, false);
                    var data = new LogicalData
                    {
                        Version = (uint)Marshal.SizeOf<LogicalData>() | (1u << 16), OsAdapterId = luidBuffer,
                        PhysicalHandles = new nint[64], Reserved = new uint[8]
                    };
                    if (info(logicalGpu, ref data) != 0 || data.OsAdapterId != luidBuffer || data.PhysicalCount != 1 ||
                        data.PhysicalHandles[0] != handle) continue; // Never combine linked physical GPUs.
                    var key = Marshal.PtrToStructure<GpuLuid>(luidBuffer).Key;
                    if (adapters.Count(a => a.VendorId == 0x10de && a.Key == key) == 1) candidates.Add((key, handle));
                }
            }
            finally { Marshal.FreeHGlobal(luidBuffer); }
            foreach (var group in candidates.GroupBy(c => c.Key).Where(g => g.Count() == 1)) handles[group.Key] = group.Single().Handle;
            ready = handles.Count > 0;
        }
        catch { } // Fixed unavailable outcome; no native errors or IDs are persisted.
        finally { if (!ready) Dispose(); }
    }

    public IReadOnlyDictionary<string, RawGpuUsage> Read()
    {
        var result = new Dictionary<string, RawGpuUsage>();
        if (disposed || read is null) return result;
        foreach (var (key, handle) in handles)
        {
            var started = clock();
            double? percent = null;
            try
            {
                var data = new States { Version = (uint)Marshal.SizeOf<States>() | (1u << 16), Domains = new Domain[8] };
                if (read(handle, ref data) == 0 && (data.Domains[0].Present & 1) != 0 && data.Domains[0].Percent <= 100)
                    percent = data.Domains[0].Percent;
            }
            catch { }
            result[key] = new(percent, started, clock());
        }
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; handles.Clear();
        try { if (initialized) unload?.Invoke(); }
        finally { initialized = false; library.Dispose(); }
    }

    [StructLayout(LayoutKind.Sequential)] internal struct Domain { public uint Present, Percent; }
    [StructLayout(LayoutKind.Sequential)] internal struct States
    {
        public uint Version, Flags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public Domain[] Domains;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct LogicalData
    {
        public uint Version;
        public nint OsAdapterId;
        public uint PhysicalCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public nint[] PhysicalHandles;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Reserved;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate nint QueryInterface(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Simple();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Enumerate(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = 64)] nint[] handles, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LogicalHandle(nint physical, out nint logical);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LogicalInfo(nint logical, ref LogicalData data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int DynamicStates(nint gpu, ref States data);
}
