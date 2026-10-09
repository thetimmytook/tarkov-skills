using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

// ABI checked against ADLX SDK 2.0.0.125, commit 32b5a740d42295c5dfe9026b9f52683da0f3af91.
// Only GPU identity, capability and tracked usage are queried. No power/tuning APIs.
internal sealed class AdlxGpuUsage : IGpuUsageProvider
{
    private static readonly SemaphoreSlim RuntimeOwner = new(1, 1);
    private readonly IWindowsDriverLibrary library;
    private readonly Func<double> clock;
    private readonly Dictionary<string, nint> gpus = [];
    private AdlxCalls.Terminate? terminate;
    private nint service;
    private bool lease, initialized, tracking, disposed;
    public string Source => GpuUsageSources.Amd;

    internal AdlxGpuUsage(IReadOnlyList<AdapterReading> adapters, IWindowsDriverLibrary? native = null, Func<double>? clock = null)
    {
        library = native ?? new WindowsDriverLibrary("amdadlx64.dll");
        this.clock = clock ?? ResourceSampler.Clock;
        bool ready = false;
        try
        {
            if (!RuntimeOwner.Wait(0)) return;
            lease = true;
            T Export<T>(string name) where T : Delegate
            {
                var address = library.Export(name);
                return address == 0 ? throw new InvalidOperationException() : Marshal.GetDelegateForFunctionPointer<T>(address);
            }
            terminate = Export<AdlxCalls.Terminate>("ADLXTerminate");
            if (Export<AdlxCalls.FullVersion>("ADLXQueryFullVersion")(out var version) != 0 || version == 0) return;
            if (Export<AdlxCalls.Initialize>("ADLXInitialize")(version, out var system) != 0 || system == 0) return;
            initialized = true; // An already-initialized foreign runtime is not ours to terminate.
            if (AdlxCalls.Method<AdlxCalls.Object>(system, 9)(system, out service) != 0 || service == 0) return;
            var candidates = Enumerate(system, adapters);
            foreach (var group in candidates.GroupBy(c => c.Key))
            {
                if (group.Count() == 1) gpus[group.Key] = group.Single().Pointer;
                else foreach (var candidate in group) AdlxCalls.Release(candidate.Pointer);
            }
            if (gpus.Count == 0 || AdlxCalls.Method<AdlxCalls.Reference>(service, 11)(service) != 0) return;
            tracking = true; ready = true;
        }
        catch { }
        finally { if (!ready) Dispose(); }
    }

    private List<(string Key, nint Pointer)> Enumerate(nint system, IReadOnlyList<AdapterReading> adapters)
    {
        var candidates = new List<(string Key, nint Pointer)>();
        nint list = 0;
        try
        {
            if (AdlxCalls.Method<AdlxCalls.Object>(system, 1)(system, out list) != 0 || list == 0) return candidates;
            uint count = AdlxCalls.Method<AdlxCalls.Count>(list, 3)(list);
            if (count > 32) return candidates;
            for (uint index = 0; index < count; index++)
            {
                nint gpu = 0, gpu2 = 0, caps = 0;
                bool retained = false;
                try
                {
                    if (AdlxCalls.Method<AdlxCalls.At>(list, 11)(list, index, out gpu) != 0 || gpu == 0) continue;
                    if (AdlxCalls.Method<AdlxCalls.Query>(gpu, 2)(gpu, "IADLXGPU2", out gpu2) != 0 || gpu2 == 0) continue;
                    if (AdlxCalls.Method<AdlxCalls.Luid>(gpu2, 34)(gpu2, out var luid) != 0 ||
                        adapters.Count(a => a.VendorId == 0x1002 && a.Key == luid.Key) != 1) continue;
                    // Linked AMD adapters are unsupported, not averaged across physical GPUs.
                    if (AdlxCalls.Method<AdlxCalls.Integer>(gpu2, 21)(gpu2, out var multiGpu) != 0 || multiGpu != 0) continue;
                    if (AdlxCalls.Method<AdlxCalls.Support>(service, 21)(service, gpu, out caps) != 0 || caps == 0 ||
                        AdlxCalls.Method<AdlxCalls.Boolean>(caps, 3)(caps, out var supported) != 0 || supported == 0) continue;
                    candidates.Add((luid.Key, gpu)); retained = true;
                }
                finally { AdlxCalls.Release(caps); AdlxCalls.Release(gpu2); if (!retained) AdlxCalls.Release(gpu); }
            }
            return candidates;
        }
        catch
        {
            foreach (var gpu in candidates) AdlxCalls.Release(gpu.Pointer);
            throw;
        }
        finally { AdlxCalls.Release(list); }
    }

    public IReadOnlyDictionary<string, RawGpuUsage> Read()
    {
        var values = new Dictionary<string, RawGpuUsage>();
        if (disposed || !tracking) return values;
        foreach (var (key, gpu) in gpus)
        {
            var started = clock();
            double? usage = null; long? stamp = null;
            nint list = 0, metric = 0;
            try
            {
                // Newest tracked sample; do not clear or alter another consumer's history.
                if (AdlxCalls.Method<AdlxCalls.History>(service, 14)(service, gpu, 0, 0, out list) == 0 && list != 0)
                {
                    uint count = AdlxCalls.Method<AdlxCalls.Count>(list, 3)(list);
                    if (count is > 0 and <= 4096 && AdlxCalls.Method<AdlxCalls.At>(list, 11)(list, count - 1, out metric) == 0 && metric != 0 &&
                        AdlxCalls.Method<AdlxCalls.Timestamp>(metric, 3)(metric, out var timestamp) == 0)
                    {
                        stamp = timestamp;
                        if (AdlxCalls.Method<AdlxCalls.Usage>(metric, 4)(metric, out var percent) == 0 &&
                            double.IsFinite(percent) && percent is >= 0 and <= 100) usage = percent;
                    }
                }
            }
            catch { }
            finally { AdlxCalls.Release(metric); AdlxCalls.Release(list); }
            values[key] = new(usage, started, clock(), stamp);
        }
        return values;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (tracking) { tracking = false; AdlxCalls.Method<AdlxCalls.Reference>(service, 12)(service); }
        }
        finally
        {
            try
            {
                foreach (var gpu in gpus.Values) AdlxCalls.Release(gpu);
                gpus.Clear(); AdlxCalls.Release(service); service = 0;
            }
            finally
            {
                try { if (initialized) { initialized = false; terminate?.Invoke(); } }
                finally { library.Dispose(); if (lease) { lease = false; RuntimeOwner.Release(); } }
            }
        }
    }
}
