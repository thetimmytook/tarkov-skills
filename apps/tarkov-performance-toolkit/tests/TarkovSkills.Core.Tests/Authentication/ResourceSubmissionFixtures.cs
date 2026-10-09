namespace TarkovSkills.Core.Tests.Authentication;

internal static class ResourceSubmissionFixtures
{
    internal const string GpuName = "NVIDIA GeForce RTX 4070";
    internal const double GiB = 1073741824;

    internal static ResourceTelemetry Collected(bool partial = false, bool unified = false, bool ambiguous = false)
    {
        var inventory = new ResourceInventory(32 * GiB, 31 * GiB, true,
            ambiguous ? [new("a", GpuName, 12 * GiB, false), new("b", "Other GPU", 8 * GiB, false)] : [new("a", GpuName, 12 * GiB, unified)],
            new Dictionary<string, string> { [@"C:\private\pagefile.sys"] = "SSD", [@"D:\private\swap.sys"] = "HDD" });
        var samples = Enumerable.Range(0, 121).Where(i => !partial || i % 4 != 0).Select(i => new ResourceSample(i, i - 1, i,
            new Dictionary<string, double?> { ["_Total"] = 25, ["0,0"] = 98 },
            new(new Dictionary<string, GpuReading> { ["a"] = new(90, 11.7 * GiB, GiB), ["b"] = new(10, GiB, 0) }, ambiguous ? ["a", "b"] : ["a"]),
            new(31 * GiB, 3 * GiB, 35 * GiB, 55 * GiB),
            [new(@"C:\private\pagefile.sys", (16 + i / 60) * GiB, 2 * GiB), new(@"D:\private\swap.sys", 8 * GiB, GiB)])).ToArray();
        return ResourceSummary.Build(samples, inventory, CaptureWindow.FromFrames([new(0, 120)]), 120);
    }

    internal static BenchmarkRun Run(ResourceTelemetry telemetry) => SubmissionTests.Run() with { ResourceTelemetry = telemetry };

    internal static ResourceTelemetry Large(int processors = 128, bool manyReasons = false)
    {
        var telemetry = Collected();
        var metric = telemetry.Cpu.LogicalProcessors[0].Utilization;
        string[] codes = ["not_collected", "counter_unavailable", "window_unavailable", "summary_unavailable", "collector_unavailable",
            "active_adapter_unknown", "multiple_active_adapters", "linked_adapter_unsupported", "memory_architecture_unknown", "pagefile_management_unknown"];
        if (manyReasons) metric = metric with { Average = null, Minimum = null, Maximum = null, Last = null, ValidSampleCount = 0,
            ValidDurationSec = 0, Coverage = 0, Status = "unavailable", ReasonCodes = codes };
        return telemetry with { Cpu = telemetry.Cpu with { LogicalProcessors = Enumerable.Range(0, processors).Select(i => new LogicalProcessorTelemetry(i / 64, i % 64, metric)).ToArray() },
            Status = manyReasons ? "partial" : telemetry.Status, Warnings = manyReasons ? codes : telemetry.Warnings };
    }
}
