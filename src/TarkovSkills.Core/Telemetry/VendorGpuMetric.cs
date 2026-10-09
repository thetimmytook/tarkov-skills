namespace TarkovSkills.Core;

internal static class VendorGpuMetric
{
    internal static ResourceMetric Build(IReadOnlyList<ResourceSample> samples, string key, string source,
        CaptureWindow window, string missingReason)
    {
        var readings = new List<(double? Value, double Seconds)>();
        double priorEnd = double.NegativeInfinity;
        foreach (var sample in samples)
        {
            if (sample.VendorGpu?.GetValueOrDefault(key) is not { } reading || reading.Start < priorEnd ||
                reading.End - reading.Start > 1.000001 || !window.Contains(reading.Start, reading.End) ||
                !window.Contains(reading.AcquisitionStart, reading.End)) continue;
            readings.Add((reading.Percent, reading.End - reading.Start)); priorEnd = reading.End;
        }
        return ResourceSummary.Summarize(readings, window.Duration, "percent", source, "whole_adapter", missingReason);
    }
}
