using System.Text.Json.Serialization;

namespace TarkovSkills.Core;

// Only these summaries cross the persistence/clipboard/command boundary. Raw samples,
// counter instance names, adapter LUIDs, process IDs and pagefile paths stay internal.
public sealed record ResourceMetric(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Average,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Minimum,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Maximum,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Last,
    int ValidSampleCount, double ValidDurationSec, double Coverage,
    string Unit, string Source, string Scope, string Status, IReadOnlyList<string> ReasonCodes);

public sealed record ResourceCapacity(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Value,
    string Unit, string Source, string Scope, string Status, IReadOnlyList<string> ReasonCodes);

public sealed record ResourceWindow(int RequestedDurationSec,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? DurationSec,
    double TargetIntervalSec, int ExpectedSampleCount, string Alignment, string CoverageMethod);
public sealed record LogicalProcessorTelemetry(int Group, int Index, ResourceMetric Utilization);
public sealed record CpuTelemetry(ResourceMetric TotalUtilization, IReadOnlyList<LogicalProcessorTelemetry> LogicalProcessors);
public sealed record GpuTelemetry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AdapterName,
    string Scope, string MemoryArchitecture, string SelectionMethod, string SelectionStatus,
    ResourceCapacity DedicatedVramCapacity, ResourceMetric GraphicsUtilization,
    ResourceMetric DedicatedMemoryUsed, ResourceMetric SharedMemoryUsed);
public sealed record RamTelemetry(ResourceCapacity InstalledCapacity, ResourceCapacity OsUsableCapacity,
    ResourceMetric PhysicalUsed, ResourceMetric PhysicalAvailable);
public sealed record PagefileTelemetry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool? AutomaticManagement,
    ResourceMetric FileCount, ResourceMetric Allocated, ResourceMetric Used,
    IReadOnlyList<PagefileResource> Files)
{
    public string AutomaticManagementSource => "win32_computer_system_automatic_managed_pagefile";
    public string AutomaticManagementScope => "system_policy";
}
public sealed record PagefileResource(int Index, string DriveMediaType, ResourceMetric Allocated, ResourceMetric Used);
public sealed record CommitTelemetry(ResourceMetric Used, ResourceMetric Limit, ResourceMetric Headroom);

public sealed record ResourceTelemetry(int SchemaVersion, string Status, ResourceWindow Window,
    CpuTelemetry Cpu, GpuTelemetry Gpu, RamTelemetry Ram, PagefileTelemetry Pagefile,
    CommitTelemetry Commit, IReadOnlyList<string> Warnings)
{
    public static ResourceTelemetry NotCollected { get; } = Unavailable(0, "not_collected") with { Status = "not_collected" };

    internal static ResourceTelemetry Unavailable(int duration, string reason)
    {
        // Emergency/legacy construction must not invoke the aggregation code that
        // may have just failed, including indirectly through summary helpers.
        ResourceMetric Missing(string unit, string source, string scope = "whole_system", string? missing = null) =>
            new(null, null, null, null, 0, 0, 0, unit, source, scope, "unavailable", [missing ?? reason]);
        ResourceCapacity Capacity(string source, string scope = "whole_system", string? missing = null) =>
            new(null, "bytes", source, scope, "unavailable", [missing ?? reason]);
        const string gpuReason = "active_adapter_unknown";
        return new(1, "unavailable", new(duration, null, 1, 0, "unknown", "valid_interval_duration_gauges_capped_at_one_second"),
            new(Missing("percent", "pdh_processor_information_processor_time"), []),
            new(null, "whole_adapter", "unknown", "unknown", "unknown",
                Capacity("dxgi_dedicated_video_memory", "whole_adapter", gpuReason),
                Missing("percent", "pdh_gpu_engine_3d_busiest_engine", "whole_adapter", gpuReason),
                Missing("bytes", "pdh_gpu_adapter_memory_dedicated", "whole_adapter", gpuReason),
                Missing("bytes", "pdh_gpu_adapter_memory_shared", "whole_adapter", gpuReason)),
            new(Capacity("get_physically_installed_system_memory"), Capacity("get_performance_info_physical_total"),
                Missing("bytes", "get_performance_info"), Missing("bytes", "get_performance_info")),
            new(null, Missing("count", "enum_page_files"), Missing("bytes", "enum_page_files"), Missing("bytes", "enum_page_files"), []),
            new(Missing("bytes", "get_performance_info"), Missing("bytes", "get_performance_info"), Missing("bytes", "get_performance_info")),
            new[] { reason, gpuReason, "pagefile_management_unknown" }.Distinct().ToArray());
    }
}
