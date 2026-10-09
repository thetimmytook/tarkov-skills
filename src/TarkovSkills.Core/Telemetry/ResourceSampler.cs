using System.Diagnostics;
using System.Management;

namespace TarkovSkills.Core;

// Owns all native handles on one worker. No trace or private instance name is saved.
internal sealed class ResourceSampler(int gamePid, int durationSeconds) : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly List<ResourceSample> samples = [];
    private ResourceInventory inventory = new(null, null, null, [], new Dictionary<string, string>());
    private Task? worker;
    private string? failure;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task StartAsync(CancellationToken cancellation = default)
    {
        worker = Task.Run(RunAsync);
        await ready.Task.WaitAsync(cancellation).ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            inventory = ReadInventory();
            using var counters = new WindowsPerformanceCounters();
            using var vendorGpu = new VendorGpuSampler(inventory.Adapters);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            counters.Collect();
            vendorGpu.Poll(); // Prime history/timestamps before the FPS capture starts.
            var previous = Clock();
            ready.TrySetResult();
            // Bounded even if an external capture fails to terminate as requested.
            for (var i = 0; i < durationSeconds + 60 && await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false); i++)
            {
                var collected = false;
                try { collected = counters.Collect(); } catch { }
                var counterTime = Clock();
                var cpu = SafeCounters("cpu").GroupBy(c => c.Instance).ToDictionary(g => g.Key, g => g.Count() == 1 ? g.First().Value : null);
                var gpu = new GpuSample(new Dictionary<string, GpuReading>(), []);
                try { gpu = GpuCounters.Parse(SafeCounters("graphics"), SafeCounters("dedicated"), SafeCounters("shared"), gamePid); } catch { }
                var vendor = vendorGpu.Poll();
                MemoryReading? memory = null;
                IReadOnlyList<PagefileReading>? pagefiles = null;
                try { memory = WindowsResourceMemory.ReadMemory(); } catch { }
                try { pagefiles = WindowsResourceMemory.ReadPagefiles(); } catch { }
                samples.Add(new(Clock(), previous, counterTime, cpu, gpu, memory, pagefiles, vendor));
                previous = counterTime;

                IReadOnlyList<CounterReading> SafeCounters(string key)
                {
                    try { return collected ? counters.Read(key) : []; } catch { return []; }
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { failure = "collector_unavailable"; } // Never expose native errors/instance paths.
        finally { ready.TrySetResult(); }
    }

    public async Task<ResourceTelemetry> CompleteAsync(CaptureWindow window)
    {
        await StopAsync().ConfigureAwait(false);
        try { return ResourceSummary.Build(samples, inventory, window, durationSeconds, failure); }
        catch { return ResourceTelemetry.Unavailable(durationSeconds, "summary_unavailable"); }
    }

    private async Task StopAsync()
    {
        stop.Cancel();
        if (worker is not null) await worker.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        samples.Clear();
        stop.Dispose();
    }

    internal static double Clock() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private static ResourceInventory ReadInventory()
    {
        double? installed = null, usable = null;
        IReadOnlyList<AdapterReading> adapters = [];
        bool? automatic = null;
        var media = new Dictionary<string, string>();
        try { installed = WindowsResourceMemory.InstalledRam(); } catch { }
        try { usable = WindowsResourceMemory.ReadMemory()?.PhysicalTotal; } catch { }
        try { adapters = WindowsGpuInventory.Read(); } catch { }
        try
        {
            using var query = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem");
            query.Options.Timeout = TimeSpan.FromSeconds(2);
            using var results = query.Get();
            foreach (ManagementObject item in results)
                using (item) { automatic = item["AutomaticManagedPagefile"] as bool?; break; }
        }
        catch { }
        // Storage queries are metadata preparation, never part of the 1 Hz worker.
        try
        {
            foreach (var file in WindowsResourceMemory.ReadPagefiles() ?? [])
                media[file.Key] = HardwareInfo.GetDriveMediaType(file.Key);
        }
        catch { }
        return new(installed, usable, automatic, adapters, media);
    }
}
