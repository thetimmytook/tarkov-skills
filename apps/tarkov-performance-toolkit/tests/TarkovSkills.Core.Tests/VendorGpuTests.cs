using System.Runtime.InteropServices;
using System.Text.Json;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Tests.Authentication;

namespace TarkovSkills.Core.Tests;

public sealed class VendorGpuTests
{
    private static readonly CaptureWindow Window = CaptureWindow.FromFrames([new(10, 13)]);
    private static ResourceSample Sample(double time, VendorGpuReading? vendor = null) => new(time, time - 1, time,
        new Dictionary<string, double?>(), new(new Dictionary<string, GpuReading> { ["a"] = new(1, 7, 2) }, ["a"]),
        new(32, 8, 40, 60), [], vendor is null ? null : new Dictionary<string, VendorGpuReading> { ["a"] = vendor });
    private static ResourceInventory Inventory(uint vendor) => new(32, 32, true, [new("a", "Test GPU", 8, false, vendor)], new Dictionary<string, string>());

    [Fact]
    public void NativeLayoutsAndFixedSourceSelectionMatchPinnedAbis()
    {
        Assert.Equal(8, Marshal.SizeOf<GpuLuid>());
        Assert.Equal(72, Marshal.SizeOf<NvApiGpuUsage.States>());
        Assert.Equal(568, Marshal.SizeOf<NvApiGpuUsage.LogicalData>());
        Assert.Equal(8, Marshal.OffsetOf<NvApiGpuUsage.LogicalData>(nameof(NvApiGpuUsage.LogicalData.OsAdapterId)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<NvApiGpuUsage.LogicalData>(nameof(NvApiGpuUsage.LogicalData.PhysicalHandles)).ToInt32());
        Assert.Equal(GpuUsageSources.Nvidia, GpuUsageSources.For(0x10de));
        Assert.Equal(GpuUsageSources.Amd, GpuUsageSources.For(0x1002));
        Assert.Equal(GpuUsageSources.Windows, GpuUsageSources.For(0x8086));
    }

    [Fact]
    public void UntimestampedDriverObservationsPrimeAndCapGapsWithoutRequiringChangingValues()
    {
        var observer = new GpuUsageObservations(false);
        Assert.Null(observer.Accept(new(100, 0, .001)));
        var first = Assert.IsType<VendorGpuReading>(observer.Accept(new(100, 1, 1.001)));
        Assert.Equal(100, first.Percent); Assert.Equal(.001, first.Start, 8); Assert.Equal(0, first.AcquisitionStart);
        var delayed = Assert.IsType<VendorGpuReading>(observer.Accept(new(0, 8, 8.001)));
        Assert.Equal(0, delayed.Percent); Assert.Equal(1, delayed.End - delayed.Start, 8);
    }

    [Theory]
    [InlineData(-1)] [InlineData(101)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidPercentagesStayUnknownAndNeverBecomeZero(double value)
    {
        var observer = new GpuUsageObservations(false);
        observer.Accept(new(30, 0, .001));
        Assert.Null(observer.Accept(new(value, 1, 1.001)));
        Assert.Equal(0, observer.Accept(new(0, 2, 2.001))!.Percent);
    }

    [Fact]
    public void AmdTimestampsExcludePrimingDuplicatesRegressionsAndClockDiscontinuities()
    {
        var observer = new GpuUsageObservations(true);
        Assert.Null(observer.Accept(new(99, 0, .001, 1000)));
        Assert.Null(observer.Accept(new(99, 1, 1.001, 1000)));
        Assert.Null(observer.Accept(new(99, 2, 2.001, 900)));
        Assert.Null(observer.Accept(new(99, 3, 3.001, 999999))); // Driver/observer clocks diverged.
        Assert.Equal(20, observer.Accept(new(20, 4, 4.001, 1000999))!.Percent);
        Assert.Null(observer.Accept(new(80, 5, 5.001, null)));
        Assert.Null(observer.Accept(new(80, 6, 6.001, 1002999))); // Re-prime after timestamp failure.
        Assert.Equal(80, observer.Accept(new(80, 7, 7.001, 1003999))!.Percent);
    }

    [Fact]
    public void LongNativeQueriesAndBrokenQpcBoundsAreRejected()
    {
        var observer = new GpuUsageObservations(false);
        observer.Accept(new(20, 0, .001));
        Assert.Null(observer.Accept(new(99, 1, 3)));
        Assert.Null(observer.Accept(new(99, 2, 2.1)));
        Assert.Null(observer.Accept(new(99, double.NaN, 4)));
    }

    [Theory]
    [InlineData(0x10deu, GpuUsageSources.Nvidia)] [InlineData(0x1002u, GpuUsageSources.Amd)]
    public void VendorSummaryReplacesOnlyUtilizationAndNeverFallsBackToWindowsLoad(uint vendor, string source)
    {
        var telemetry = ResourceSummary.Build([Sample(11, new(99, 10, 11, 10)), Sample(12, new(50, 11, 12, 11))], Inventory(vendor), Window, 120);
        Assert.Equal(source, telemetry.Gpu.GraphicsUtilization.Source);
        Assert.Equal(74.5, telemetry.Gpu.GraphicsUtilization.Average);
        Assert.Equal(2, telemetry.Gpu.GraphicsUtilization.ValidSampleCount);
        Assert.Equal(2, telemetry.Gpu.GraphicsUtilization.ValidDurationSec);
        Assert.Equal(.666667, telemetry.Gpu.GraphicsUtilization.Coverage);
        Assert.Equal(7, telemetry.Gpu.DedicatedMemoryUsed.Average); Assert.Equal(2, telemetry.Gpu.SharedMemoryUsed.Average);
        Assert.Equal("pdh_gpu_adapter_memory_dedicated", telemetry.Gpu.DedicatedMemoryUsed.Source);
        var missing = ResourceSummary.Build([Sample(11)], Inventory(vendor), Window, 120);
        Assert.Equal(source, missing.Gpu.GraphicsUtilization.Source); Assert.Null(missing.Gpu.GraphicsUtilization.Average);
        Assert.Contains("counter_unavailable", missing.Gpu.GraphicsUtilization.ReasonCodes);
    }

    [Fact]
    public void WindowBoundariesGapsOverlapsAndDuplicateObservationsDoNotEnterPeaksOrCoverage()
    {
        var telemetry = ResourceSummary.Build([
            Sample(9, new(100, 8, 9, 8)), Sample(10.2, new(100, 10, 10.2, 9.2)),
            Sample(11, new(40, 10, 11, 10)), Sample(11.1, new(100, 10, 11, 10)),
            Sample(12, new(60, 11, 12, 11)), Sample(14, new(100, 13, 14, 13))], Inventory(0x10de), Window, 120);
        Assert.Equal(60, telemetry.Gpu.GraphicsUtilization.Maximum);
        Assert.Equal(2, telemetry.Gpu.GraphicsUtilization.ValidSampleCount);
        var gaps = CaptureWindow.FromFrames([new(10, 10.5), new(11, 13)]);
        Assert.Equal(1, ResourceSummary.Build([Sample(11, new(40, 10, 11, 10)), Sample(12, new(60, 11, 12, 11))],
            Inventory(0x10de), gaps, 120).Gpu.GraphicsUtilization.ValidSampleCount);
    }

    [Fact]
    public void GameAdapterSelectionIsPreservedWhenAnotherVendorGpuIsBusier()
    {
        var sample = Sample(11, new(20, 10, 11, 10)) with
        { VendorGpu = new Dictionary<string, VendorGpuReading> { ["a"] = new(20, 10, 11, 10), ["b"] = new(100, 10, 11, 10) } };
        var inventory = Inventory(0x10de) with { Adapters = [new("a", "Game GPU", 8, false, 0x10de), new("b", "Other GPU", 16, false, 0x1002)] };
        var summary = ResourceSummary.Build([sample], inventory, Window, 120);
        Assert.Equal("Game GPU", summary.Gpu.AdapterName); Assert.Equal(20, summary.Gpu.GraphicsUtilization.Average);
        Assert.Equal(GpuUsageSources.Nvidia, summary.Gpu.GraphicsUtilization.Source);
        var ambiguous = sample with { Gpu = sample.Gpu with { GameAdapters = ["a", "b"] } };
        Assert.Null(ResourceSummary.Build([ambiguous], inventory, Window, 120).Gpu.GraphicsUtilization.Average);
    }

    [Fact]
    public void ProviderFailureDoesNotStopAnotherProviderOrLeakInternalAdapterIds()
    {
        var nvidia = new FakeProvider(GpuUsageSources.Nvidia, "a");
        var amd = new FakeProvider(GpuUsageSources.Amd, "b");
        using (var sampler = new VendorGpuSampler([new("a", "GPU", 8, false, 0x10de), new("b", "GPU", 8, false, 0x1002)],
            (source, _) => source == nvidia.Source ? nvidia : amd))
        {
            Assert.Empty(sampler.Poll());
            nvidia.Fail = true;
            Assert.Single(sampler.Poll());
            Assert.Equal("b", sampler.Poll().Keys.Single());
        }
        Assert.Equal(1, nvidia.DisposeCount); Assert.Equal(1, amd.DisposeCount);
        var summary = ResourceSummary.Build([Sample(11, new(99, 10, 11, 10))], Inventory(0x10de), Window, 120);
        var json = JsonSerializer.Serialize(summary, JsonDefaults.Options);
        Assert.DoesNotContain("driver_timestamp", json); Assert.DoesNotContain("vendor_gpu", json);
        Assert.DoesNotContain("acquisition_start", json); Assert.DoesNotContain("luid", json);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<ResourceTelemetry>(json, JsonDefaults.Options), JsonDefaults.Options));
    }

    [Theory]
    [InlineData(GpuUsageSources.Windows)] [InlineData(GpuUsageSources.Nvidia)] [InlineData(GpuUsageSources.Amd)]
    public void SupportedGraphicsSourcesPersistWithoutRelabellingOrChangingTheSummary(string source)
    {
        var telemetry = ResourceSubmissionFixtures.Collected();
        telemetry = telemetry with { Gpu = telemetry.Gpu with { GraphicsUtilization = telemetry.Gpu.GraphicsUtilization with { Source = source } } };
        var run = ResourceSubmissionFixtures.Run(telemetry);
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var prepared = new SubmissionOutbox(directory, new Uri("https://example.invalid"), "test", "test").Prepare(run, ResourceSubmissionFixtures.GpuName);
            Assert.Equal(JsonSerializer.Serialize(telemetry, JsonDefaults.Options), JsonSerializer.Serialize(prepared.ResourceTelemetry, JsonDefaults.Options));
            Assert.Equal(source, run.ResourceTelemetry.Gpu.GraphicsUtilization.Source);
            var file = Assert.Single(Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories));
            Assert.Equal(prepared.Json, File.ReadAllText(file));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class FakeProvider(string source, string key) : IGpuUsageProvider
    {
        private int tick;
        public string Source => source;
        internal bool Fail;
        internal int DisposeCount;
        public IReadOnlyDictionary<string, RawGpuUsage> Read()
        {
            if (Fail) throw new InvalidOperationException("private native error");
            tick++; return new Dictionary<string, RawGpuUsage> { [key] = new(80, tick, tick + .001, tick * 1000) };
        }
        public void Dispose() => DisposeCount++;
    }
}
