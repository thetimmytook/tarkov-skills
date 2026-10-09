using System.Text.Json;
using static TarkovSkills.Core.Academy.ResourceMetricContract;

namespace TarkovSkills.Core.Academy;

// Explicit shape validation precedes deserialization: missing nullable fields,
// duplicate/unknown keys and read-only metadata cannot disappear during reprojection.
internal static class ResourceTelemetryContract
{
    internal static ResourceTelemetry Read(JsonElement root, double fpsDuration, int frameCount)
    {
        Object(root, "schema_version", "status", "window", "cpu", "gpu", "ram", "pagefile", "commit", "warnings");
        Require(Number(root.GetProperty("schema_version"), 1, true) == 1);
        Choice(root.GetProperty("status"), "available", "partial", "unavailable", "not_collected");
        Shape(root);
        var telemetry = root.Deserialize<ResourceTelemetry>(JsonDefaults.Options)!;
        Relations(telemetry, fpsDuration, frameCount);
        return telemetry;
    }

    private static void Shape(JsonElement root)
    {
        var window = root.GetProperty("window");
        Object(window, "requested_duration_sec", "duration_sec", "target_interval_sec", "expected_sample_count", "alignment", "coverage_method");
        Require(new[] { 0d, 120, 240 }.Contains(Number(window.GetProperty("requested_duration_sec"), 240, true)));
        Require(NullableNumber(window.GetProperty("duration_sec"), double.MaxValue) is null or > 0);
        Require(Number(window.GetProperty("target_interval_sec"), 1) == 1);
        Number(window.GetProperty("expected_sample_count"), int.MaxValue, true);
        Choice(window.GetProperty("alignment"), "unknown", "presentmon_qpc_valid_frame_intervals");
        Choice(window.GetProperty("coverage_method"), "valid_interval_duration_gauges_capped_at_one_second");
        var cpu = root.GetProperty("cpu"); Object(cpu, "total_utilization", "logical_processors");
        CpuMetric(cpu.GetProperty("total_utilization"), "whole_system");
        foreach (var item in Array(cpu.GetProperty("logical_processors"), 512))
        {
            Object(item, "group", "index", "utilization");
            Number(item.GetProperty("group"), 65535, true); Number(item.GetProperty("index"), 63, true);
            CpuMetric(item.GetProperty("utilization"), "logical_processor");
        }
        var gpu = root.GetProperty("gpu");
        Object(gpu, "adapter_name", "scope", "memory_architecture", "selection_method", "selection_status",
            "dedicated_vram_capacity", "graphics_utilization", "dedicated_memory_used", "shared_memory_used");
        var name = gpu.GetProperty("adapter_name");
        Require(name.ValueKind == JsonValueKind.Null || name.ValueKind == JsonValueKind.String &&
            ResourceSummary.SafeAdapterName(name.GetString()!) == name.GetString());
        Choice(gpu.GetProperty("scope"), "whole_adapter");
        Choice(gpu.GetProperty("memory_architecture"), "discrete", "unified", "unknown");
        Choice(gpu.GetProperty("selection_method"), "tarkov_graphics_activity", "single_hardware_adapter", "unknown");
        Choice(gpu.GetProperty("selection_status"), "selected", "unknown");
        Capacity(gpu.GetProperty("dedicated_vram_capacity"), "whole_adapter", "dxgi_dedicated_video_memory", "d3d12_unified_memory_no_discrete_vram");
        Metric(gpu.GetProperty("graphics_utilization"), "percent", GpuUsageSources.Windows, "whole_adapter", GpuUsageSources.Nvidia, GpuUsageSources.Amd);
        Metric(gpu.GetProperty("dedicated_memory_used"), "bytes", "pdh_gpu_adapter_memory_dedicated", "whole_adapter");
        Metric(gpu.GetProperty("shared_memory_used"), "bytes", "pdh_gpu_adapter_memory_shared", "whole_adapter");
        var ram = root.GetProperty("ram"); Object(ram, "installed_capacity", "os_usable_capacity", "physical_used", "physical_available");
        Capacity(ram.GetProperty("installed_capacity"), "whole_system", "get_physically_installed_system_memory");
        Capacity(ram.GetProperty("os_usable_capacity"), "whole_system", "get_performance_info_physical_total");
        MemoryMetric(ram.GetProperty("physical_used")); MemoryMetric(ram.GetProperty("physical_available"));
        var pagefile = root.GetProperty("pagefile");
        Object(pagefile, "automatic_management", "automatic_management_source", "automatic_management_scope", "file_count", "allocated", "used", "files");
        Require(pagefile.GetProperty("automatic_management").ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null);
        Choice(pagefile.GetProperty("automatic_management_source"), "win32_computer_system_automatic_managed_pagefile");
        Choice(pagefile.GetProperty("automatic_management_scope"), "system_policy");
        Metric(pagefile.GetProperty("file_count"), "count", "enum_page_files", "whole_system");
        PagefileMetric(pagefile.GetProperty("allocated")); PagefileMetric(pagefile.GetProperty("used"));
        foreach (var file in Array(pagefile.GetProperty("files"), 32))
        {
            Object(file, "index", "drive_media_type", "allocated", "used");
            Require(Number(file.GetProperty("index"), 32, true) >= 1);
            Choice(file.GetProperty("drive_media_type"), "HDD", "SSD", "SCM", "unknown");
            PagefileMetric(file.GetProperty("allocated"), "pagefile"); PagefileMetric(file.GetProperty("used"), "pagefile");
        }
        var commit = root.GetProperty("commit"); Object(commit, "used", "limit", "headroom");
        MemoryMetric(commit.GetProperty("used")); MemoryMetric(commit.GetProperty("limit")); MemoryMetric(commit.GetProperty("headroom"));
        ReasonCodes(root.GetProperty("warnings"));
    }

    private static IEnumerable<JsonElement> Array(JsonElement node, int maximum)
    {
        Require(node.ValueKind == JsonValueKind.Array && node.GetArrayLength() <= maximum);
        return node.EnumerateArray();
    }
    private static void CpuMetric(JsonElement node, string scope) => Metric(node, "percent", "pdh_processor_information_processor_time", scope);
    private static void MemoryMetric(JsonElement node) => Metric(node, "bytes", "get_performance_info", "whole_system");
    private static void PagefileMetric(JsonElement node, string scope = "whole_system") => Metric(node, "bytes", "enum_page_files", scope);

    private static void Relations(ResourceTelemetry t, double fpsDuration, int frameCount)
    {
        var w = t.Window;
        ResourceMetric[] core = [t.Cpu.TotalUtilization, t.Gpu.GraphicsUtilization, t.Gpu.DedicatedMemoryUsed, t.Gpu.SharedMemoryUsed,
            t.Ram.PhysicalUsed, t.Ram.PhysicalAvailable, t.Pagefile.FileCount, t.Pagefile.Allocated, t.Pagefile.Used,
            t.Commit.Used, t.Commit.Limit, t.Commit.Headroom];
        var all = core.Concat(t.Cpu.LogicalProcessors.Select(p => p.Utilization)).Concat(t.Pagefile.Files.SelectMany(f => new[] { f.Allocated, f.Used })).ToArray();
        ResourceCapacity[] capacities = [t.Ram.InstalledCapacity, t.Ram.OsUsableCapacity, t.Gpu.DedicatedVramCapacity];
        Require(t.Cpu.LogicalProcessors.Select(p => (p.Group, p.Index)).Distinct().Count() == t.Cpu.LogicalProcessors.Count);
        Require(t.Pagefile.Files.Select(f => f.Index).Distinct().Count() == t.Pagefile.Files.Count);
        Require(w.DurationSec is null ? w.Alignment == "unknown" && w.ExpectedSampleCount == 0 :
            w.Alignment == "presentmon_qpc_valid_frame_intervals" && w.ExpectedSampleCount >= Math.Ceiling(w.DurationSec.Value - DurationTolerance) &&
            w.ExpectedSampleCount <= Math.Ceiling(w.DurationSec.Value + DurationTolerance));
        if (w.DurationSec is { } duration)
            Require(duration <= fpsDuration + .0005 + DurationTolerance + (frameCount - 1) * .000001 + FloatingSlack);
        foreach (var metric in all.Where(m => m.Status != "unavailable"))
        {
            Require(w.DurationSec.HasValue);
            Coverage(metric, w.DurationSec!.Value);
        }
        if (t.Status == "not_collected")
            Require(w.RequestedDurationSec == 0 && w.DurationSec is null && all.All(m => m.Status == "unavailable") &&
                capacities.All(c => c.Status == "unavailable") && t.Cpu.LogicalProcessors.Count == 0 && t.Pagefile.Files.Count == 0 &&
                t.Pagefile.AutomaticManagement is null && t.Warnings.Contains("not_collected"));
        else
        {
            var expected = core.All(m => m.Status == "unavailable") ? "unavailable" :
                core.All(m => m.Status == "available") && capacities.All(c => c.Status == "available") &&
                t.Pagefile.AutomaticManagement.HasValue && t.Cpu.LogicalProcessors.Count > 0 &&
                t.Cpu.LogicalProcessors.All(p => p.Utilization.Status == "available") ? "available" : "partial";
            Require(w.RequestedDurationSec != 0 && t.Status == expected);
        }
        var warnings = core.SelectMany(m => m.ReasonCodes).Concat(t.Cpu.LogicalProcessors.SelectMany(p => p.Utilization.ReasonCodes))
            .Concat(capacities.SelectMany(c => c.ReasonCodes)).Concat(t.Pagefile.AutomaticManagement is null ? new[] { "pagefile_management_unknown" } : []).ToHashSet();
        Require(warnings.SetEquals(t.Warnings));
        Gpu(t.Gpu);
        var usable = t.Ram.OsUsableCapacity.Value;
        Require(t.Ram.InstalledCapacity.Value is null || usable is null || usable <= t.Ram.InstalledCapacity.Value);
        Require(usable is null || new[] { t.Ram.PhysicalUsed.Maximum, t.Ram.PhysicalAvailable.Maximum }.All(v => v is null || v <= usable));
    }

    private static void Coverage(ResourceMetric metric, double duration)
    {
        var earliest = Math.Max(0, metric.ValidDurationSec - DurationTolerance) / (duration + DurationTolerance);
        var latest = (metric.ValidDurationSec + DurationTolerance) / Math.Max(double.Epsilon, duration - DurationTolerance);
        Require(metric.ValidDurationSec <= duration + 2 * DurationTolerance &&
            earliest <= metric.Coverage + CoverageTolerance + FloatingSlack && latest >= metric.Coverage - CoverageTolerance - FloatingSlack &&
            (metric.Status != "available" || latest >= 1 - CompleteTolerance - FloatingSlack) &&
            (metric.Status != "partial" || earliest < 1 - CompleteTolerance + FloatingSlack));
    }

    private static void Gpu(GpuTelemetry gpu)
    {
        var capacity = gpu.DedicatedVramCapacity;
        Require(gpu.SelectionStatus == "unknown" ? gpu.SelectionMethod == "unknown" && gpu.AdapterName is null &&
            gpu.MemoryArchitecture == "unknown" && capacity.Status == "unavailable" &&
            new[] { gpu.GraphicsUtilization, gpu.DedicatedMemoryUsed, gpu.SharedMemoryUsed }.All(m => m.Status == "unavailable") : gpu.SelectionMethod != "unknown");
        Require(gpu.MemoryArchitecture == "unified" ? capacity.Source == "d3d12_unified_memory_no_discrete_vram" &&
            capacity.Status == "available" && capacity.Value == 0 : capacity.Source == "dxgi_dedicated_video_memory");
        Require(gpu.MemoryArchitecture != "unknown" || capacity.Value is null);
        Require(gpu.MemoryArchitecture != "discrete" || capacity.Value is null || capacity.Value > 0 && (gpu.DedicatedMemoryUsed.Maximum ?? 0) <= capacity.Value);
    }
}
