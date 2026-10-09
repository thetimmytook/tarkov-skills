using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TarkovSkills.Core;

namespace TarkovBenchmark.Feature;

public partial class ResourceTelemetryView : UserControl
{
    private sealed record Row(string Label, string Value, string Coverage, string Detail);
    public ResourceTelemetryView() => InitializeComponent();

    public void Show(ResourceTelemetry telemetry)
    {
        ResourceDetails.IsEnabled = telemetry.Status != "not_collected";
        ResourceDetails.Header = telemetry.Status == "not_collected" ? "Resource telemetry · not collected for this run" :
            $"Resource telemetry · {telemetry.Status}";
        if (telemetry.Status == "not_collected") { ResourceDetails.IsExpanded = false; return; }
        WindowText.Text = telemetry.Window.DurationSec is { } seconds ?
            $"Valid FPS window: {seconds:0.0} s · resource coverage is shown for each metric." :
            "The resource samples could not be aligned with valid FPS timestamps. FPS results remain available.";
        GpuHeading.Text = $"GPU: {telemetry.Gpu.AdapterName ?? "unknown"} · whole adapter · {telemetry.Gpu.MemoryArchitecture} memory";
        var rows = new List<Row>
        {
            Metric("CPU utilization", telemetry.Cpu.TotalUtilization),
            Metric(telemetry.Gpu.GraphicsUtilization.Source is "nvapi_gpu_graphics_utilization" or "adlx_gpu_usage"
                ? "GPU graphics load (vendor driver)" : "GPU graphics utilization (Windows 3D)", telemetry.Gpu.GraphicsUtilization),
            Capacity("Physical dedicated VRAM", telemetry.Gpu.DedicatedVramCapacity),
            Metric("Dedicated GPU memory used", telemetry.Gpu.DedicatedMemoryUsed),
            Metric("Shared GPU memory used", telemetry.Gpu.SharedMemoryUsed),
            Capacity("RAM installed", telemetry.Ram.InstalledCapacity),
            Capacity("RAM usable by Windows", telemetry.Ram.OsUsableCapacity),
            Metric("Physical RAM used", telemetry.Ram.PhysicalUsed),
            Metric("Physical RAM available", telemetry.Ram.PhysicalAvailable, true),
            Metric("Pagefiles", telemetry.Pagefile.FileCount),
            Metric("Pagefile allocated", telemetry.Pagefile.Allocated),
            Metric("Pagefile used", telemetry.Pagefile.Used),
            Metric("System commit used", telemetry.Commit.Used),
            Metric("System commit limit", telemetry.Commit.Limit),
            Metric("Commit headroom", telemetry.Commit.Headroom, true)
        };
        foreach (var file in telemetry.Pagefile.Files)
        {
            rows.Add(Metric($"Pagefile {file.Index} allocated ({file.DriveMediaType})", file.Allocated));
            rows.Add(Metric($"Pagefile {file.Index} used", file.Used));
        }
        ResourceRows.ItemsSource = rows;
        ProcessorRows.ItemsSource = telemetry.Cpu.LogicalProcessors.Select(p =>
            Metric($"Group {p.Group}, logical CPU {p.Index}", p.Utilization)).ToArray();
        ProcessorDetails.Header = telemetry.Cpu.LogicalProcessors.Count == 0 ?
            "Logical CPU utilization · unavailable" : "Logical CPU utilization · whole system";
        var management = telemetry.Pagefile.AutomaticManagement switch { true => "enabled", false => "disabled", _ => "unknown" };
        MemoryExplanation.Text = $"Windows automatic management of all pagefiles: {management}. Individual pagefiles can still be system-managed when this is disabled. Allocated pagefile size can change during a capture. " +
            "Commit is memory Windows has committed to support; its limit is measured separately from pagefile size. " +
            "Shared GPU memory uses system RAM and adds no physical VRAM. On unified-memory GPUs, dedicated GPU usage may be reserved system RAM.";
        TextureHint.Visibility = HasTextureTestHint(telemetry.Gpu) ? Visibility.Visible : Visibility.Collapsed;
        TextureHint.Text = "Sampled dedicated VRAM reached at least 95% of capacity. This is a potential limitation and a reason to try lower texture quality in a repeated A/B test; it does not establish the cause of FPS drops.";
    }

    internal static bool HasTextureTestHint(GpuTelemetry gpu) => gpu.MemoryArchitecture == "discrete" &&
        gpu.DedicatedVramCapacity.Value is > 0 && gpu.DedicatedMemoryUsed.Maximum is { } peak &&
        peak <= gpu.DedicatedVramCapacity.Value && peak / gpu.DedicatedVramCapacity.Value >= .95;

    private static Row Metric(string label, ResourceMetric metric, bool minimum = false)
    {
        var value = metric.Average is null ? "Unavailable" :
            $"avg {Value(metric.Average, metric.Unit)} · {(minimum ? "min" : "max")} {Value(minimum ? metric.Minimum : metric.Maximum, metric.Unit)} · last {Value(metric.Last, metric.Unit)}";
        return new(label, value, $"{metric.Coverage:P0} coverage\n{metric.ValidSampleCount} readings",
            $"Source: {metric.Source}\nScope: {metric.Scope}\n" + string.Join("\n", metric.ReasonCodes.Select(Reason)));
    }
    private static Row Capacity(string label, ResourceCapacity capacity) => new(label, Value(capacity.Value, capacity.Unit),
        capacity.Status == "available" ? "capacity" : "unavailable",
        $"Source: {capacity.Source}\nScope: {capacity.Scope}\n" + string.Join("\n", capacity.ReasonCodes.Select(Reason)));
    private static string Value(double? value, string unit) => value is not { } number ? "Unavailable" : unit switch
    {
        "bytes" => (number / 1073741824d).ToString("0.00", CultureInfo.CurrentCulture) + " GiB",
        "percent" => number.ToString("0.0", CultureInfo.CurrentCulture) + "%",
        _ => number.ToString("0.0", CultureInfo.CurrentCulture)
    };
    private static string Reason(string code) => code switch
    {
        "partial_coverage" => "Some intervals were unavailable or outside the valid FPS window.",
        "active_adapter_unknown" => "The adapter used for this run could not be identified reliably.",
        "multiple_active_adapters" => "Multiple game graphics adapters were observed; values were not combined.",
        "linked_adapter_unsupported" => "Linked physical GPU nodes could not be measured separately and were not combined.",
        "window_unavailable" => "Valid FPS timestamps were unavailable for alignment.",
        "memory_architecture_unknown" => "Windows could not distinguish dedicated VRAM from unified memory reliably.",
        "pagefile_management_unknown" => "Windows pagefile management policy was unavailable.",
        _ => "The resource reading was unavailable or could not be matched to the selected adapter."
    };
}
