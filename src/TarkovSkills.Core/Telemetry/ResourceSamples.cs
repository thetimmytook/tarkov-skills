namespace TarkovSkills.Core;

internal sealed record MemoryReading(double PhysicalTotal, double Available, double CommitUsed, double CommitLimit);
internal sealed record PagefileReading(string Key, double Allocated, double Used);
internal sealed record AdapterReading(string Key, string Name, double DedicatedVideoCapacity, bool? UnifiedMemory = false,
    uint VendorId = 0);
internal sealed record CounterReading(string Instance, double? Value);
internal sealed record GpuReading(double? Graphics, double? Dedicated, double? Shared);
internal sealed record GpuSample(IReadOnlyDictionary<string, GpuReading> Adapters, IReadOnlyList<string> GameAdapters,
    IReadOnlyList<string>? UnsupportedAdapters = null);
internal sealed record ResourceSample(double Time, double CpuIntervalStart, double CpuIntervalEnd,
    IReadOnlyDictionary<string, double?> Cpu, GpuSample Gpu, MemoryReading? Memory,
    IReadOnlyList<PagefileReading>? Pagefiles, IReadOnlyDictionary<string, VendorGpuReading>? VendorGpu = null);
internal sealed record ResourceInventory(double? InstalledRam, double? UsableRam, bool? AutomaticPagefile,
    IReadOnlyList<AdapterReading> Adapters, IReadOnlyDictionary<string, string> PagefileMedia);

internal readonly record struct CaptureInterval(double Start, double End);
internal sealed record CaptureWindow(IReadOnlyList<CaptureInterval> Intervals)
{
    public static CaptureWindow Unknown { get; } = new([]);
    public double Duration => Intervals.Sum(i => i.End - i.Start);

    public static CaptureWindow FromFrames(IEnumerable<CaptureInterval> frames)
    {
        var merged = new List<CaptureInterval>();
        foreach (var frame in frames.Where(i => double.IsFinite(i.Start) && double.IsFinite(i.End) && i.End > i.Start).OrderBy(i => i.Start))
        {
            if (merged.Count > 0 && frame.Start <= merged[^1].End + .000001)
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, frame.End) };
            else merged.Add(frame);
        }
        return new(merged);
    }

    // A gauge supports at most one sampling period; gaps are never filled by a stale value.
    public double GaugeSupport(double time) => Intervals.Where(i => time >= i.Start && time < i.End)
        .Select(i => Math.Min(1, i.End - time)).FirstOrDefault();
    // Rate counters describe the entire delta interval. Do not pretend that a delta
    // spanning preparation, a missing frame interval, or capture end belongs to the run.
    public bool Contains(double start, double end) => end > start && end - start <= 1.5 &&
        Intervals.Any(i => start >= i.Start && end <= i.End);
}
