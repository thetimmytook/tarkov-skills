using System.Text.Json;

namespace TarkovSkills.Core.Tests;

public sealed class GpuCountersTests
{
    [Fact]
    public void EnginesAggregateProcessesWithinOneEngineThenSelectBusiestGraphicsEngine()
    {
        var gpu = GpuCounters.Parse([
            new("pid_42_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", 10),
            new("pid_43_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", 20),
            new("pid_44_luid_0x00000000_0x00000001_phys_0_eng_1_engtype_3D", 80),
            new("pid_42_luid_0x00000000_0x00000002_phys_0_eng_0_engtype_Copy", 100)],
            [new("luid_0x00000000_0x00000001_phys_0", 4)], [new("luid_0x00000000_0x00000001_phys_0", 2)], 42);
        var key = GpuCounters.Key(0, 1);
        Assert.Equal(80, gpu.Adapters[key].Graphics);
        Assert.Equal(4, gpu.Adapters[key].Dedicated);
        Assert.Equal(2, gpu.Adapters[key].Shared);
        Assert.Equal(new[] { key }, gpu.GameAdapters);
        var sample = new ResourceSample(11, 10, 11, new Dictionary<string, double?>(), gpu, null, null);
        var summary = ResourceSummary.Build([sample], new(null, null, null, [new(key, "GPU", 8)], new Dictionary<string, string>()),
            CaptureWindow.FromFrames([new(10, 12)]), 120);
        var json = JsonSerializer.Serialize(summary, JsonDefaults.Options);
        Assert.DoesNotContain("pid_", json);
        Assert.DoesNotContain("luid_", json);
        Assert.DoesNotContain(key, json);
        Assert.DoesNotContain("\"process_id\"", json);
        Assert.DoesNotContain("\"process_name\"", json);
    }

    [Fact]
    public void InvalidNewInstancesDoNotBecomeZerosAndPhysicalNodesAreNotSummed()
    {
        var key = GpuCounters.Key(0, 1);
        var gpu = GpuCounters.Parse([
            new("pid_42_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", 50),
            new("pid_43_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", null)],
            [new("luid_0x00000000_0x00000001_phys_0", 4), new("luid_0x00000000_0x00000001_phys_1", 8)], [], 42);
        Assert.Null(gpu.Adapters[key].Graphics);
        Assert.Null(gpu.Adapters[key].Dedicated);
        Assert.Contains(key, gpu.UnsupportedAdapters!);
    }
}
