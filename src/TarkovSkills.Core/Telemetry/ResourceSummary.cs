using System.Globalization;

namespace TarkovSkills.Core;

internal static class ResourceSummary
{
    internal static ResourceTelemetry Build(IReadOnlyList<ResourceSample> samples, ResourceInventory inventory,
        CaptureWindow window, int requestedSeconds, string? unavailableReason = null)
    {
        var aligned = window.Duration > 0;
        var reason = unavailableReason ?? (aligned ? "counter_unavailable" : "window_unavailable");
        ResourceMetric Gauge(Func<ResourceSample, double?> read, string unit, string source, string scope = "whole_system", string? missing = null) =>
            Summarize(samples.Select((s, i) => (read(s), Math.Min(window.GaugeSupport(s.Time),
                i + 1 < samples.Count ? Math.Max(0, samples[i + 1].Time - s.Time) : 1))), window.Duration,
                unit, source, scope, missing ?? reason);
        ResourceMetric Rate(Func<ResourceSample, double?> read, string source, string scope, string? missing = null) =>
            Summarize(samples.Select(s => (window.Contains(s.CpuIntervalStart, s.CpuIntervalEnd) ? read(s) : null,
                window.Contains(s.CpuIntervalStart, s.CpuIntervalEnd) ? s.CpuIntervalEnd - s.CpuIntervalStart : 0)),
                window.Duration, "percent", source, scope, missing ?? reason);

        var cpu = new CpuTelemetry(Rate(s => s.Cpu.GetValueOrDefault("_Total"), "pdh_processor_information_processor_time", "whole_system"),
            samples.SelectMany(s => s.Cpu.Keys).Distinct().Select(ParseProcessor).Where(p => p is not null)
                .Select(p => p!.Value).OrderBy(p => p.Group).ThenBy(p => p.Index)
                .Select(p => new LogicalProcessorTelemetry(p.Group, p.Index,
                    Rate(s => s.Cpu.GetValueOrDefault(p.Key), "pdh_processor_information_processor_time", "logical_processor"))).ToArray());

        // Selection evidence is restricted to readings inside this run. Never use the
        // busiest unrelated adapter, add GPUs together, or mix changing game adapters.
        var evidence = samples.Where(s => window.Contains(s.CpuIntervalStart, s.CpuIntervalEnd))
            .SelectMany(s => s.Gpu.GameAdapters).Distinct().ToArray();
        AdapterReading? adapter = null;
        var selection = "unknown";
        var gpuReason = "active_adapter_unknown";
        if (evidence.Length == 1)
        {
            adapter = inventory.Adapters.SingleOrDefault(a => a.Key == evidence[0]);
            if (adapter is not null) selection = "tarkov_graphics_activity";
        }
        else if (evidence.Length == 0 && inventory.Adapters.Count == 1)
        {
            adapter = inventory.Adapters[0];
            selection = "single_hardware_adapter";
        }
        if (evidence.Length > 1) gpuReason = "multiple_active_adapters";
        if (adapter is not null && samples.Where(s => window.GaugeSupport(s.Time) > 0)
                .Any(s => s.Gpu.UnsupportedAdapters?.Contains(adapter.Key) == true))
        {
            adapter = null;
            selection = "unknown";
            gpuReason = "linked_adapter_unsupported";
        }
        double? GpuValue(ResourceSample sample, Func<GpuReading, double?> read) => adapter is null ? null :
            sample.Gpu.Adapters.TryGetValue(adapter.Key, out var value) ? read(value) : null;
        var architecture = adapter?.UnifiedMemory switch { true => "unified", false => "discrete", _ => "unknown" };
        var capacity = adapter?.UnifiedMemory switch { true => 0d, false when adapter.DedicatedVideoCapacity > 0 => adapter.DedicatedVideoCapacity, _ => (double?)null };
        var graphicsSource = GpuUsageSources.For(adapter?.VendorId ?? 0);
        var graphics = GpuUsageSources.IsVendor(graphicsSource)
            ? VendorGpuMetric.Build(samples, adapter!.Key, graphicsSource, window, reason)
            : Rate(s => GpuValue(s, v => v.Graphics), GpuUsageSources.Windows, "whole_adapter", adapter is null ? gpuReason : reason);
        var gpu = new GpuTelemetry(adapter is null ? null : SafeAdapterName(adapter.Name), "whole_adapter", architecture, selection,
            adapter is null ? "unknown" : "selected",
            Capacity(capacity, architecture == "unified" ? "d3d12_unified_memory_no_discrete_vram" : "dxgi_dedicated_video_memory", "whole_adapter",
                adapter is null ? gpuReason : architecture == "unknown" ? "memory_architecture_unknown" : reason),
            graphics,
            Gauge(s => GpuValue(s, v => v.Dedicated is { } d && (capacity is null or 0 || d <= capacity) ? d : null),
                "bytes", "pdh_gpu_adapter_memory_dedicated", "whole_adapter", adapter is null ? gpuReason : reason),
            Gauge(s => GpuValue(s, v => v.Shared), "bytes", "pdh_gpu_adapter_memory_shared", "whole_adapter", adapter is null ? gpuReason : reason));

        var ram = new RamTelemetry(Capacity(inventory.InstalledRam, "get_physically_installed_system_memory", "whole_system", reason),
            Capacity(inventory.UsableRam, "get_performance_info_physical_total", "whole_system", reason),
            Gauge(s => s.Memory is { } m ? m.PhysicalTotal - m.Available : null, "bytes", "get_performance_info"),
            Gauge(s => s.Memory?.Available, "bytes", "get_performance_info"));
        var fileKeys = samples.Where(s => window.GaugeSupport(s.Time) > 0).SelectMany(s => s.Pagefiles ?? []).Select(f => f.Key).Distinct().ToArray();
        double? SumFiles(ResourceSample s, Func<PagefileReading, double> read) => s.Pagefiles is null ? null : s.Pagefiles.Sum(read);
        var pagefile = new PagefileTelemetry(inventory.AutomaticPagefile,
            Gauge(s => s.Pagefiles?.Count, "count", "enum_page_files"),
            Gauge(s => SumFiles(s, p => p.Allocated), "bytes", "enum_page_files"),
            Gauge(s => SumFiles(s, p => p.Used), "bytes", "enum_page_files"),
            fileKeys.Select((key, index) => new PagefileResource(index + 1, inventory.PagefileMedia.GetValueOrDefault(key, "unknown"),
                Gauge(s => s.Pagefiles?.FirstOrDefault(p => p.Key == key)?.Allocated, "bytes", "enum_page_files", "pagefile"),
                Gauge(s => s.Pagefiles?.FirstOrDefault(p => p.Key == key)?.Used, "bytes", "enum_page_files", "pagefile"))).ToArray());
        var commit = new CommitTelemetry(Gauge(s => s.Memory?.CommitUsed, "bytes", "get_performance_info"),
            Gauge(s => s.Memory?.CommitLimit, "bytes", "get_performance_info"),
            Gauge(s => s.Memory is { } m ? m.CommitLimit - m.CommitUsed : null, "bytes", "get_performance_info"));
        ResourceMetric[] metrics = [cpu.TotalUtilization, gpu.GraphicsUtilization, gpu.DedicatedMemoryUsed, gpu.SharedMemoryUsed,
            ram.PhysicalUsed, ram.PhysicalAvailable, pagefile.FileCount, pagefile.Allocated, pagefile.Used, commit.Used, commit.Limit, commit.Headroom];
        var status = metrics.All(m => m.Status == "unavailable") ? "unavailable" :
            metrics.All(m => m.Status == "available") && inventory.InstalledRam.HasValue && inventory.UsableRam.HasValue &&
                inventory.AutomaticPagefile.HasValue && gpu.DedicatedVramCapacity.Value.HasValue && cpu.LogicalProcessors.Count > 0 &&
                cpu.LogicalProcessors.All(p => p.Utilization.Status == "available") ? "available" : "partial";
        var warnings = metrics.SelectMany(m => m.ReasonCodes).Concat(cpu.LogicalProcessors.SelectMany(p => p.Utilization.ReasonCodes))
            .Concat(ram.InstalledCapacity.ReasonCodes).Concat(ram.OsUsableCapacity.ReasonCodes).Concat(gpu.DedicatedVramCapacity.ReasonCodes)
            .Concat(inventory.AutomaticPagefile.HasValue ? [] : new[] { "pagefile_management_unknown" }).Distinct().ToArray();
        return new(1, status, new(requestedSeconds, aligned ? Math.Round(window.Duration, 6) : null, 1,
            aligned ? (int)Math.Ceiling(window.Duration) : 0, aligned ? "presentmon_qpc_valid_frame_intervals" : "unknown",
            "valid_interval_duration_gauges_capped_at_one_second"), cpu, gpu, ram, pagefile, commit, warnings);
    }

    internal static ResourceMetric Summarize(IEnumerable<(double? Value, double Seconds)> readings, double windowSeconds,
        string unit, string source, string scope, string missingReason)
    {
        var valid = readings.Where(p => p.Value is { } v && double.IsFinite(v) && v >= 0 &&
            (unit != "percent" || v <= 100) && p.Seconds > 0 && double.IsFinite(p.Seconds)).ToArray();
        var seconds = valid.Sum(p => p.Seconds);
        // Online weighted mean avoids summing large repeated byte counts (which can
        // drift beyond the sampled extrema). The final clamp only bounds FP rounding
        // of the mathematical mean; saved/submitted statistics are never repaired.
        double mean = 0, weight = 0;
        foreach (var reading in valid)
        {
            weight += reading.Seconds;
            mean += (reading.Value!.Value - mean) * (reading.Seconds / weight);
        }
        var minimum = valid.Length == 0 ? (double?)null : valid.Min(p => p.Value);
        var maximum = valid.Length == 0 ? (double?)null : valid.Max(p => p.Value);
        var coverage = windowSeconds > 0 ? Math.Clamp(seconds / windowSeconds, 0, 1) : 0;
        var status = valid.Length == 0 ? "unavailable" : coverage >= 1 - .000001 ? "available" : "partial";
        return new(valid.Length == 0 ? null : Math.Clamp(mean, minimum!.Value, maximum!.Value),
            minimum, maximum,
            valid.Length == 0 ? null : valid[^1].Value, valid.Length, Math.Round(seconds, 6), Math.Round(coverage, 6),
            unit, source, scope, status, status == "available" ? [] : new[] { valid.Length == 0 ? missingReason : "partial_coverage" });
    }

    private static ResourceCapacity Capacity(double? value, string source, string scope, string reason) =>
        value is { } v && double.IsFinite(v) && v >= 0 ? new(v, "bytes", source, scope, "available", []) :
            new(null, "bytes", source, scope, "unavailable", [reason]);

    private static (string Key, int Group, int Index)? ParseProcessor(string key)
    {
        var parts = key.Split(',');
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? (key, group, index) : null;
    }

    internal static string? SafeAdapterName(string name)
    {
        var sanitized = PrivacySanitizer.SanitizeText(name);
        return sanitized.Length is > 0 and <= 160 && !sanitized.Any(char.IsControl) &&
            !System.Text.RegularExpressions.Regex.IsMatch(sanitized, @"(?i)\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b|\b(?:\d{1,3}\.){3}\d{1,3}\b") &&
            !sanitized.Contains(':') && !sanitized.Contains('@') &&
            !sanitized.Contains('\\') && !sanitized.Contains('/') ? sanitized : null;
    }
}
