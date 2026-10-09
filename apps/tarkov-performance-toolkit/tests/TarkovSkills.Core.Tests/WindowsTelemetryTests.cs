using System.Diagnostics;
using System.Text.Json;
using Xunit.Abstractions;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Tests.Authentication;

namespace TarkovSkills.Core.Tests;

// Read-only native smoke/overhead checks. No ETW session, game process, app-owned
// history, upload, or permission change is needed. Real FPS A/B remains a flight gate.
public sealed class WindowsTelemetryTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "WindowsTelemetry")]
    public async Task NativeCountersMemoryAndDxgiCanBeSampledWithSanitizedPartialResults()
    {
        var preparation = Stopwatch.StartNew();
        var adapters = WindowsGpuInventory.Read();
        var memory = WindowsResourceMemory.ReadMemory();
        var installed = WindowsResourceMemory.InstalledRam();
        var pagefiles = WindowsResourceMemory.ReadPagefiles();
        using var counters = new WindowsPerformanceCounters();
        using var vendor = new VendorGpuSampler(adapters);
        vendor.Poll();
        counters.Collect();
        preparation.Stop();
        var samples = new List<ResourceSample>();
        var durations = new List<double>();
        // Warm the interop/regex paths before measuring steady polling overhead.
        _ = counters.Read("cpu");
        _ = GpuCounters.Parse(counters.Read("graphics"), counters.Read("dedicated"), counters.Read("shared"), int.MaxValue);
        _ = WindowsResourceMemory.ReadPagefiles();
        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var allocatedBefore = GC.GetTotalAllocatedBytes();
        var previous = ResourceSampler.Clock();
        var start = previous;
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(1000);
            var clock = Stopwatch.StartNew();
            var collected = counters.Collect();
            var time = ResourceSampler.Clock();
            var cpu = collected ? counters.Read("cpu").ToDictionary(c => c.Instance, c => c.Value) : new Dictionary<string, double?>();
            var gpu = GpuCounters.Parse(counters.Read("graphics"), counters.Read("dedicated"), counters.Read("shared"), int.MaxValue);
            var vendorGpu = vendor.Poll();
            samples.Add(new(ResourceSampler.Clock(), previous, time, cpu, gpu,
                WindowsResourceMemory.ReadMemory(), WindowsResourceMemory.ReadPagefiles(), vendorGpu));
            previous = time;
            clock.Stop();
            durations.Add(clock.Elapsed.TotalMilliseconds);
        }
        var cpuDelta = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        var allocatedDelta = GC.GetTotalAllocatedBytes() - allocatedBefore;
        var summary = ResourceSummary.Build(samples, new(installed, memory?.PhysicalTotal, null, adapters, new Dictionary<string, string>()),
            CaptureWindow.FromFrames([new(start, ResourceSampler.Clock() + .01)]), 120);
        Assert.NotNull(memory);
        Assert.True(memory.PhysicalTotal >= memory.Available);
        Assert.True(memory.CommitLimit >= memory.CommitUsed);
        Assert.True(summary.Ram.PhysicalAvailable.ValidSampleCount > 0);
        if (Environment.GetEnvironmentVariable("TARKOV_VALIDATE_NVIDIA_NATIVE") == "1")
        {
            Assert.Equal(GpuUsageSources.Nvidia, summary.Gpu.GraphicsUtilization.Source);
            Assert.True(summary.Gpu.GraphicsUtilization.ValidSampleCount > 0);
        }
        if (pagefiles is not null) Assert.All(pagefiles, file => Assert.True(file.Allocated >= file.Used));
        var json = JsonSerializer.Serialize(summary, JsonDefaults.Options);
        Assert.DoesNotContain("luid_", json);
        Assert.DoesNotContain("pid_", json);
        Assert.DoesNotContain("pagefile.sys", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        // Validate actual native summaries at the submission boundary. FPS is a
        // synthetic fixture here; this is not a game capture or an uploaded run.
        var fixture = SubmissionTests.Run() with { ResourceTelemetry = summary };
        var submission = SubmissionPayload.Create(fixture, ResourceSubmissionFixtures.GpuName);
        SubmissionPayload.ValidateStored(submission, Guid.Parse(fixture.RunId));
        ResourceSubmissionTests.Export("native-summary-with-synthetic-fps", submission);
        output.WriteLine(JsonSerializer.Serialize(new {
            preparation_elapsed_ms = preparation.Elapsed.TotalMilliseconds,
            poll_average_elapsed_ms = durations.Average(), poll_max_elapsed_ms = durations.Max(),
            six_poll_process_cpu_ms = cpuDelta,
            six_poll_allocated_bytes = allocatedDelta,
            cpu_samples = summary.Cpu.TotalUtilization.ValidSampleCount,
            gpu_graphics_samples = summary.Gpu.GraphicsUtilization.ValidSampleCount,
            gpu_graphics_source = summary.Gpu.GraphicsUtilization.Source,
            dedicated_memory_samples = summary.Gpu.DedicatedMemoryUsed.ValidSampleCount,
            shared_memory_samples = summary.Gpu.SharedMemoryUsed.ValidSampleCount,
            gpu_capacity_status = summary.Gpu.DedicatedVramCapacity.Status,
            ram_samples = summary.Ram.PhysicalAvailable.ValidSampleCount,
            pagefile_samples = summary.Pagefile.Used.ValidSampleCount
        }));
    }

    [Fact]
    [Trait("Category", "WindowsTelemetry")]
    public async Task SamplerStopsOnCancellationWithoutSavingOrPublishingAnything()
    {
        await using var sampler = new ResourceSampler(int.MaxValue, 120);
        await sampler.StartAsync();
        var start = ResourceSampler.Clock();
        await Task.Delay(2200);
        var clock = Stopwatch.StartNew();
        var summary = await sampler.CompleteAsync(CaptureWindow.FromFrames([new(start, ResourceSampler.Clock())]));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2));
        Assert.True(summary.Ram.PhysicalAvailable.ValidSampleCount > 0);
        Assert.Equal("whole_adapter", summary.Gpu.Scope);
    }
}
