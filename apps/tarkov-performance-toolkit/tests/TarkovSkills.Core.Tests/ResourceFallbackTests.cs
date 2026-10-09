using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Tests.Authentication;

namespace TarkovSkills.Core.Tests;

public sealed class ResourceFallbackTests
{
    [Fact]
    public async Task ActualAggregationFailurePreservesCompleteFpsAndProducesAValidUnavailableRequest()
    {
        var adapter = new AdapterReading("private-adapter", "private-name", 8);
        var inventory = new ResourceInventory(null, null, null, [adapter, adapter], new Dictionary<string, string>());
        var sample = new ResourceSample(11, 10, 11, new Dictionary<string, double?>(),
            new(new Dictionary<string, GpuReading>(), [adapter.Key]), null, null);
        var window = CaptureWindow.FromFrames([new(10, 130)]);
        Assert.Throws<InvalidOperationException>(() => ResourceSummary.Build([sample], inventory, window, 120));
        await using var sampler = new ResourceSampler(0, 120); // No native worker is started.
        typeof(ResourceSampler).GetField("inventory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sampler, inventory);
        ((List<ResourceSample>)typeof(ResourceSampler).GetField("samples", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sampler)!).Add(sample);
        var run = SubmissionTests.Run();
        var context = new RaidContext(true, true, "Woods", "woods", null, DateTime.Now, null);
        var captured = await CaptureLifecycle.RunAsync(120, new(
            _ => Task.FromResult(new FrameCapture(run.Performance, window)), sampler.CompleteAsync,
            () => context, () => false, () => false), CancellationToken.None);
        Assert.Equal(run.Performance, captured.Performance);
        Assert.Equal("unavailable", captured.ResourceTelemetry.Status);
        Assert.Contains("summary_unavailable", captured.ResourceTelemetry.Warnings);
        Assert.Null(captured.ResourceTelemetry.Ram.PhysicalAvailable.Minimum);
        Assert.Equal(0, captured.ResourceTelemetry.Gpu.GraphicsUtilization.Coverage);
        var json = SubmissionPayload.Create(run with { ResourceTelemetry = captured.ResourceTelemetry }, "NVIDIA GeForce RTX 4070");
        SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
        Assert.DoesNotContain("private", json);
    }

    [Theory]
    [InlineData(120, "collector_unavailable")]
    [InlineData(240, "summary_unavailable")]
    public void EmergencySummaryHasRequiredNullsAndFixedSourceScopeMetadata(int duration, string reason)
    {
        var summary = ResourceTelemetry.Unavailable(duration, reason);
        var json = JsonSerializer.SerializeToElement(summary, JsonDefaults.Options);
        ResourceTelemetryContract.Read(json, duration, 6000);
        Assert.Null(summary.Window.DurationSec);
        Assert.Equal(duration, summary.Window.RequestedDurationSec);
        Assert.Equal("whole_adapter", summary.Gpu.Scope);
        Assert.Equal("unknown", summary.Gpu.SelectionStatus);
        Assert.Empty(summary.Cpu.LogicalProcessors);
        Assert.Empty(summary.Pagefile.Files);
        Assert.Null(summary.Pagefile.AutomaticManagement);
        Assert.Contains(reason, summary.Warnings);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("commit").GetProperty("used").GetProperty("average").ValueKind);
    }

    [Fact]
    public void OldHistoryStillGetsNotCollectedWithoutManufacturingZeros()
    {
        var json = JsonSerializer.SerializeToNode(SubmissionTests.Run(), JsonDefaults.Options)!.AsObject();
        json.Remove("resource_telemetry");
        var loaded = json.Deserialize<BenchmarkRun>(JsonDefaults.Options)!;
        Assert.Equal("not_collected", loaded.ResourceTelemetry.Status);
        Assert.Null(loaded.ResourceTelemetry.Ram.InstalledCapacity.Value);
        Assert.Null(loaded.ResourceTelemetry.Cpu.TotalUtilization.Average);
        ResourceTelemetryContract.Read(JsonSerializer.SerializeToElement(loaded.ResourceTelemetry, JsonDefaults.Options), 120, 6000);
    }
}
