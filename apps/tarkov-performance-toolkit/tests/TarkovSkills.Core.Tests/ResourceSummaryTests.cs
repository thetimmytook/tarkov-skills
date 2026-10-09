using System.Text.Json;
using TarkovBenchmark.Feature;

namespace TarkovSkills.Core.Tests;

public sealed class ResourceSummaryTests
{
    private static readonly CaptureWindow Window = CaptureWindow.FromFrames([new(10, 13)]);
    private static readonly ResourceInventory Inventory = new(32, 30, true,
        [new("a", "Test GPU", 8)], new Dictionary<string, string> { [@"C:\private\pagefile.sys"] = "SSD" });

    private static ResourceSample Sample(double time, double? cpu = 25, double available = 5,
        IReadOnlyList<PagefileReading>? files = null, GpuSample? gpu = null) =>
        new(time, time - 1, time, new Dictionary<string, double?> { ["_Total"] = cpu, ["0,0"] = cpu, ["1,4"] = 90 },
            gpu ?? new(new Dictionary<string, GpuReading> { ["a"] = new(80, 7.9, 2) }, ["a"]),
            new(30, available, 40, 60), files);

    [Fact]
    public void RepeatedLargeByteCountsHaveAnAverageWithinSampledExtrema()
    {
        const double value = 11.7 * 1073741824;
        var metric = ResourceSummary.Summarize(Enumerable.Repeat(((double?)value, 1d), 240), 240, "bytes", "test", "whole_system", "counter_unavailable");
        Assert.Equal(value, metric.Average);
        Assert.Equal(value, metric.Minimum);
        Assert.Equal(value, metric.Maximum);
    }

    [Fact]
    public void UnknownDiscreteCapacityDoesNotBecomeMeasuredZero()
    {
        var inventory = Inventory with { Adapters = [new("a", "Test GPU", 0, false)] };
        var telemetry = ResourceSummary.Build([Sample(11)], inventory, Window, 120);
        Assert.Null(telemetry.Gpu.DedicatedVramCapacity.Value);
        Assert.Equal("unavailable", telemetry.Gpu.DedicatedVramCapacity.Status);
        Assert.False(ResourceTelemetryView.HasTextureTestHint(telemetry.Gpu));
    }

    [Fact]
    public void TimeWeightedAverageCountsOnlyValidSamplesAndDoesNotFillGaps()
    {
        var metric = ResourceSummary.Summarize([(10, 1), (20, .5), (null, 1), (double.NaN, .5), (-1, 1)],
            3, "bytes", "test", "whole_system", "counter_unavailable");
        Assert.Equal(2, metric.ValidSampleCount);
        Assert.Equal(1.5, metric.ValidDurationSec);
        Assert.Equal(.5, metric.Coverage);
        Assert.Equal(40d / 3, metric.Average!.Value, 6);
        Assert.Equal(20, metric.Maximum);
        Assert.Equal("partial", metric.Status);
    }

    [Fact]
    public void CapturePeaksExcludePreparationCleanupAndBoundaryCrossingRates()
    {
        var telemetry = ResourceSummary.Build([Sample(9, 100, 0), Sample(10.5, 99, 4), Sample(11.5, 20, 3),
            Sample(12.5, 30, 2), Sample(13.5, 100, 0)], Inventory, Window, 120);
        Assert.Equal(30, telemetry.Cpu.TotalUtilization.Maximum);
        Assert.Equal(2, telemetry.Cpu.TotalUtilization.ValidSampleCount);
        Assert.Equal(28, telemetry.Ram.PhysicalUsed.Maximum);
        Assert.Equal(2, telemetry.Ram.PhysicalAvailable.Minimum);
        Assert.Equal(2.5, telemetry.Ram.PhysicalAvailable.ValidDurationSec);
        Assert.Equal(3, telemetry.Window.DurationSec);
    }

    [Fact]
    public void GaugeCoverageCapsStaleSamplesAtOneSecond()
    {
        var telemetry = ResourceSummary.Build([Sample(10), Sample(12)], Inventory, Window, 120);
        Assert.Equal(2, telemetry.Ram.PhysicalAvailable.ValidDurationSec);
        Assert.Equal(.666667, telemetry.Ram.PhysicalAvailable.Coverage);
        Assert.Equal(5, telemetry.Ram.PhysicalAvailable.Average);
    }

    [Fact]
    public void MissingWindowKeepsFpsIndependentAndReportsNullNotZero()
    {
        var telemetry = ResourceSummary.Build([Sample(11)], Inventory, CaptureWindow.Unknown, 120);
        Assert.Null(telemetry.Cpu.TotalUtilization.Average);
        Assert.Null(telemetry.Pagefile.Used.Maximum);
        Assert.Equal("unavailable", telemetry.Status);
        Assert.Contains("window_unavailable", telemetry.Warnings);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(telemetry, JsonDefaults.Options));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ram").GetProperty("physical_available").GetProperty("minimum").ValueKind);
    }

    [Fact]
    public void ZeroLoadAndSuccessfullyEnumeratedEmptyPagefileAreMeasuredZeros()
    {
        var telemetry = ResourceSummary.Build([Sample(11, 0, files: [])], Inventory, Window, 120);
        Assert.Equal(0, telemetry.Cpu.TotalUtilization.Average);
        Assert.Equal(0, telemetry.Pagefile.Allocated.Average);
        Assert.Equal(0, telemetry.Pagefile.Used.Average);
        var failed = ResourceSummary.Build([Sample(11, null)], Inventory, Window, 120);
        Assert.Null(failed.Cpu.TotalUtilization.Average);
        Assert.Null(failed.Pagefile.Used.Average);
        Assert.True(failed.Ram.PhysicalAvailable.ValidSampleCount > 0);
    }

    [Fact]
    public void InstalledUsableAvailableAndCommitAreDistinctAndPagefilesMayGrow()
    {
        var telemetry = ResourceSummary.Build([
            Sample(10, files: [new(@"C:\private\pagefile.sys", 20, 4), new(@"D:\other.sys", 10, 2)]),
            Sample(11, files: [new(@"C:\private\pagefile.sys", 30, 6), new(@"D:\other.sys", 10, 3)]),
            Sample(12, files: [new(@"C:\private\pagefile.sys", 40, 8), new(@"D:\other.sys", 10, 4)])], Inventory, Window, 120);
        Assert.Equal(32, telemetry.Ram.InstalledCapacity.Value);
        Assert.Equal(30, telemetry.Ram.OsUsableCapacity.Value);
        Assert.Equal(25, telemetry.Ram.PhysicalUsed.Average);
        Assert.Equal(5, telemetry.Ram.PhysicalAvailable.Minimum);
        Assert.Equal(40, telemetry.Pagefile.Allocated.Average);
        Assert.Equal(50, telemetry.Pagefile.Allocated.Maximum);
        Assert.Equal(12, telemetry.Pagefile.Used.Maximum);
        Assert.Equal(40, telemetry.Commit.Used.Average);
        Assert.Equal(60, telemetry.Commit.Limit.Last);
        Assert.Equal(20, telemetry.Commit.Headroom.Minimum);
        Assert.True(telemetry.Pagefile.AutomaticManagement);
        Assert.Equal(2, telemetry.Pagefile.Files.Count);
        Assert.Equal("SSD", telemetry.Pagefile.Files[0].DriveMediaType);
        var json = JsonSerializer.Serialize(telemetry, JsonDefaults.Options);
        Assert.DoesNotContain("private", json);
        Assert.DoesNotContain("C:", json);
        Assert.DoesNotContain("D:", json);
    }

    [Fact]
    public void MultiGpuUsesOnlyAdapterIdentifiedByGameGraphicsActivity()
    {
        var gpu = new GpuSample(new Dictionary<string, GpuReading> { ["a"] = new(20, 3, 2), ["b"] = new(95, 12, 1) }, ["a"]);
        var telemetry = ResourceSummary.Build([Sample(11, gpu: gpu)], Inventory with
            { Adapters = [new("a", "Game GPU", 8), new("b", "Other GPU", 16)] }, Window, 120);
        Assert.Equal("Game GPU", telemetry.Gpu.AdapterName);
        Assert.Equal(3, telemetry.Gpu.DedicatedMemoryUsed.Average);
        Assert.Equal(2, telemetry.Gpu.SharedMemoryUsed.Average);
        Assert.Equal(8, telemetry.Gpu.DedicatedVramCapacity.Value);
        Assert.Equal("whole_adapter", telemetry.Gpu.DedicatedMemoryUsed.Scope);
    }

    [Fact]
    public void MultipleActiveOrUnknownGameAdaptersAreNotGuessed()
    {
        var inventory = Inventory with { Adapters = [new("a", "GPU A", 8), new("b", "GPU B", 16)] };
        foreach (var adapters in new IReadOnlyList<string>[] { ["a", "b"], [] })
        {
            var sample = Sample(11) with { Gpu = Sample(11).Gpu with { GameAdapters = adapters } };
            var telemetry = ResourceSummary.Build([sample], inventory, Window, 120);
            Assert.Null(telemetry.Gpu.AdapterName);
            Assert.Null(telemetry.Gpu.DedicatedMemoryUsed.Average);
            Assert.Equal("unknown", telemetry.Gpu.SelectionStatus);
        }
    }

    [Fact]
    public void LinkedNodesAndAdapterChangesNeverCombineMemoryOrCapacity()
    {
        var sample = Sample(11) with { Gpu = Sample(11).Gpu with { UnsupportedAdapters = ["a"] } };
        var telemetry = ResourceSummary.Build([sample], Inventory, Window, 120);
        Assert.Null(telemetry.Gpu.DedicatedVramCapacity.Value);
        Assert.Contains("linked_adapter_unsupported", telemetry.Warnings);
    }

    [Fact]
    public void UnifiedMemoryDoesNotBecomePhysicalVramOrTriggerTextureHint()
    {
        var telemetry = ResourceSummary.Build([Sample(11)], Inventory with { Adapters = [new("a", "Unified GPU", 8, true)] }, Window, 120);
        Assert.Equal(0, telemetry.Gpu.DedicatedVramCapacity.Value);
        Assert.Equal(7.9, telemetry.Gpu.DedicatedMemoryUsed.Maximum);
        Assert.False(ResourceTelemetryView.HasTextureTestHint(telemetry.Gpu));
        var unknown = ResourceSummary.Build([Sample(11)], Inventory with { Adapters = [new("a", "GPU", 8, null)] }, Window, 120);
        Assert.Null(unknown.Gpu.DedicatedVramCapacity.Value);
        Assert.Contains("memory_architecture_unknown", unknown.Warnings);
    }

    [Fact]
    public void NearCapacityOnlySuggestsTextureTestAndInvalidReadingsDoNot()
    {
        var telemetry = ResourceSummary.Build([Sample(11)], Inventory, Window, 120);
        Assert.True(ResourceTelemetryView.HasTextureTestHint(telemetry.Gpu));
        var invalid = Sample(11) with { Gpu = new(new Dictionary<string, GpuReading> { ["a"] = new(120, 999, 1) }, ["a"]) };
        var rejected = ResourceSummary.Build([invalid], Inventory, Window, 120);
        Assert.Null(rejected.Gpu.DedicatedMemoryUsed.Maximum);
        Assert.Null(rejected.Gpu.GraphicsUtilization.Maximum);
        Assert.False(ResourceTelemetryView.HasTextureTestHint(rejected.Gpu));
    }

    [Fact]
    public void ProcessorGroupsRemainDistinctAndLowTotalDoesNotHideBusyLogicalCpu()
    {
        var telemetry = ResourceSummary.Build([Sample(11, 10)], Inventory, Window, 120);
        Assert.Equal(10, telemetry.Cpu.TotalUtilization.Average);
        Assert.Equal(90, telemetry.Cpu.LogicalProcessors.Single(p => p.Group == 1 && p.Index == 4).Utilization.Maximum);
    }

    [Theory]
    [InlineData("GPU 192.168.1.2")]
    [InlineData("GPU 871e06df-5c0f-447a-a3b7-723492a2e09e")]
    [InlineData("PCI\\VEN_1234")]
    public void AdapterLabelCannotCarryIdentifiers(string label) => Assert.Null(ResourceSummary.SafeAdapterName(label));
}
