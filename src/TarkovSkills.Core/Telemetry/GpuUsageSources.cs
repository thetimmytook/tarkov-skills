namespace TarkovSkills.Core;

internal static class GpuUsageSources
{
    internal const string Windows = "pdh_gpu_engine_3d_busiest_engine";
    internal const string Nvidia = "nvapi_gpu_graphics_utilization";
    internal const string Amd = "adlx_gpu_usage";
    internal static string For(uint vendor) => vendor switch { 0x10de => Nvidia, 0x1002 => Amd, _ => Windows };
    internal static bool IsVendor(string source) => source is Nvidia or Amd;
}

// QPC observation bounds and driver timestamps never cross the summary boundary.
internal sealed record RawGpuUsage(double? Percent, double QueryStart, double QueryEnd, long? DriverTimestamp = null);
internal sealed record VendorGpuReading(double Percent, double Start, double End, double AcquisitionStart);
internal interface IGpuUsageProvider : IDisposable
{
    string Source { get; }
    IReadOnlyDictionary<string, RawGpuUsage> Read();
}

internal sealed class GpuUsageObservations(bool timestamped)
{
    private double? previousEnd, previousStampTime;
    private long? previousStamp;

    internal VendorGpuReading? Accept(RawGpuUsage value)
    {
        var priorEnd = previousEnd;
        previousEnd = double.IsFinite(value.QueryEnd) ? value.QueryEnd : null;
        bool fresh = true;
        if (timestamped)
        {
            if (value.DriverTimestamp is not > 0) { previousStamp = null; previousStampTime = null; return null; }
            var stamp = value.DriverTimestamp.Value;
            if (previousStamp is { } prior)
            {
                // Duplicate/regressed history is never counted twice. The observer clock
                // is used only to validate freshness, not to invent an ADLX epoch/QPC map.
                if (stamp <= prior) return null;
                var elapsed = value.QueryEnd - previousStampTime!.Value;
                var driverElapsed = (stamp - prior) / 1000d;
                fresh = elapsed > 0 && Math.Abs(driverElapsed - elapsed) <= .5;
            }
            else fresh = false;
            previousStamp = stamp; previousStampTime = value.QueryEnd;
        }
        if (!fresh || priorEnd is null || value.Percent is not { } percent || !double.IsFinite(percent) ||
            percent is < 0 or > 100 || !double.IsFinite(value.QueryStart) || !double.IsFinite(value.QueryEnd) ||
            value.QueryStart < priorEnd || value.QueryEnd < value.QueryStart || value.QueryEnd - value.QueryStart > 1 ||
            value.QueryEnd <= priorEnd) return null;

        // Polls support at most one second. Delays never backfill the missing interval.
        // NVAPI documents a trailing one-second domain value; ADLX's averaging period
        // is unspecified. Coverage describes observed support, not exact busy-time bins.
        return new(percent, Math.Max(priorEnd.Value, value.QueryEnd - 1), value.QueryEnd,
            timestamped ? value.QueryStart : value.QueryStart - 1);
    }
}
