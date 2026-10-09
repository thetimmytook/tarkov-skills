using System.Text.Json;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using static TarkovSkills.Core.Tests.Authentication.ResourceSubmissionFixtures;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class ResourceSubmissionTests
{
    [Theory]
    [InlineData("available")]
    [InlineData("partial")]
    [InlineData("unavailable")]
    [InlineData("not_collected")]
    [InlineData("unified")]
    [InlineData("ambiguous")]
    public void SummarySurvivesStrictProjectionAndSavedReprojection(string scenario)
    {
        var telemetry = scenario switch {
            "unavailable" => ResourceTelemetry.Unavailable(120, "collector_unavailable"),
            "not_collected" => ResourceTelemetry.NotCollected,
            "unified" => Collected(unified: true), "ambiguous" => Collected(ambiguous: true),
            _ => Collected(partial: scenario == "partial") };
        var run = Run(telemetry);
        var json = SubmissionPayload.Create(run, GpuName);
        SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
        var dto = JsonNode.Parse(json)!;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(telemetry, JsonDefaults.Options), dto["resource_telemetry"]));
        Assert.DoesNotContain("private", json); Assert.DoesNotContain("pagefile.sys", json);
        Assert.DoesNotContain("pid_", json); Assert.DoesNotContain("luid_", json);
        if (scenario is "available" or "partial" or "unavailable" or "not_collected") Assert.Equal(scenario, telemetry.Status);
        Export(scenario, json);
    }

    [Fact]
    public void SelectedHardwareGpuCannotRelabelOrChangeMeasuredAdapter()
    {
        var run = Run(Collected()) with { System = new { cpu = new { name = "Test CPU" }, ram = new { total_gb = 32 },
            gpu = new[] { new { name = GpuName }, new { name = "Other GPU" } } } };
        var dto = JsonNode.Parse(SubmissionPayload.Create(run, "Other GPU"))!;
        Assert.Equal("Other GPU", dto["hardware"]!["gpu_name"]!.GetValue<string>());
        Assert.Equal(GpuName, dto["resource_telemetry"]!["gpu"]!["adapter_name"]!.GetValue<string>());
        Assert.Equal("whole_adapter", dto["resource_telemetry"]!["gpu"]!["scope"]!.GetValue<string>());
    }

    [Fact]
    public void RequiredNullableKeysArePresentAndInvalidBlockIsNotDefaulted()
    {
        var run = Run(ResourceTelemetry.NotCollected);
        var original = JsonNode.Parse(SubmissionPayload.Create(run, GpuName))!;
        foreach (var path in new[] { "resource_telemetry.window.duration_sec", "resource_telemetry.gpu.adapter_name", "resource_telemetry.pagefile.automatic_management",
            "resource_telemetry.ram.physical_available.average", "resource_telemetry.ram.installed_capacity.value" })
        {
            var (parent, key) = At(original.DeepClone(), path);
            Assert.True(parent.ContainsKey(key)); Assert.Null(parent[key]); parent.Remove(key);
            Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(parent.Root.ToJsonString(), Guid.Parse(run.RunId)));
            Export("invalid-missing-" + path.Replace('.', '-'), parent.Root.ToJsonString());
        }
        original["resource_telemetry"] = null;
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(original.ToJsonString(), Guid.Parse(run.RunId)));
        Export("invalid-null-block", original.ToJsonString());
    }

    [Theory]
    [InlineData("resource_telemetry.pid", "42")]
    [InlineData("resource_telemetry.window.raw_samples", "[]")]
    [InlineData("resource_telemetry.cpu.logical_processors.0.process_name", "\"Other app\"")]
    [InlineData("resource_telemetry.gpu.luid", "\"0x1234\"")]
    [InlineData("resource_telemetry.gpu.graphics_utilization.source", "\"unknown_gpu_utilization\"")]
    [InlineData("resource_telemetry.gpu.graphics_utilization.source", "null")]
    [InlineData("resource_telemetry.gpu.graphics_utilization.scope", "\"game\"")]
    [InlineData("resource_telemetry.gpu.graphics_utilization.unit", "\"bytes\"")]
    [InlineData("resource_telemetry.pagefile.files.0.path", "\"C:/private/pagefile.sys\"")]
    [InlineData("resource_telemetry.cpu.total_utilization.scope", "\"game\"")]
    [InlineData("resource_telemetry.commit.used.source", "\"unknown_custom_source\"")]
    [InlineData("resource_telemetry.gpu.adapter_name", "\"GPU 192.168.1.2\"")]
    [InlineData("resource_telemetry.pagefile.automatic_management_source", "\"private_source\"")]
    [InlineData("resource_telemetry.ram.physical_available.average", "null")]
    [InlineData("resource_telemetry.cpu.total_utilization.maximum", "101")]
    [InlineData("resource_telemetry.cpu.total_utilization.minimum", "50")]
    [InlineData("resource_telemetry.ram.physical_available.last", "9999999999999")]
    [InlineData("resource_telemetry.ram.os_usable_capacity.value", "0.5")]
    [InlineData("resource_telemetry.window.duration_sec", "121")]
    [InlineData("resource_telemetry.window.expected_sample_count", "119")]
    [InlineData("resource_telemetry.window.alignment", "\"unknown\"")]
    [InlineData("resource_telemetry.cpu.total_utilization.coverage", "0.5")]
    [InlineData("resource_telemetry.cpu.total_utilization.valid_duration_sec", "121")]
    [InlineData("resource_telemetry.cpu.total_utilization.valid_sample_count", "1")]
    [InlineData("resource_telemetry.cpu.logical_processors.0.index", "64")]
    [InlineData("resource_telemetry.pagefile.files.0.index", "0")]
    [InlineData("resource_telemetry.pagefile.file_count.last", "1.5")]
    [InlineData("resource_telemetry.status", "\"unavailable\"")]
    [InlineData("resource_telemetry.status", "\"not_collected\"")]
    [InlineData("resource_telemetry.warnings", "[\"counter_unavailable\"]")]
    [InlineData("resource_telemetry.gpu.selection_status", "\"unknown\"")]
    [InlineData("resource_telemetry.gpu.memory_architecture", "\"unified\"")]
    [InlineData("resource_telemetry.gpu.dedicated_vram_capacity.value", "0")]
    [InlineData("resource_telemetry.ram.installed_capacity.value", "1")]
    [InlineData("resource_telemetry.pagefile.files.0.drive_media_type", "\"C:\"")]
    [InlineData("resource_telemetry.cpu.total_utilization.reason_codes", "[\"partial_coverage\"]")]
    [InlineData("resource_telemetry.cpu.total_utilization.valid_sample_count", "10001")]
    [InlineData("resource_telemetry.cpu.total_utilization.average", "1e999")]
    public void MalformedOrPrivateFrozenDataFailsRatherThanBeingRepaired(string path, string value)
    {
        var run = Run(Collected());
        var dto = JsonNode.Parse(SubmissionPayload.Create(run, GpuName))!;
        var (parent, key) = At(dto, path); parent[key] = JsonNode.Parse(value);
        var json = dto.ToJsonString();
        var error = Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId)));
        Assert.DoesNotContain("private", error.Message);
        Export("invalid-" + path.Replace('.', '-'), json);
    }

    [Theory]
    [InlineData(GpuUsageSources.Windows, "available")]
    [InlineData(GpuUsageSources.Windows, "partial")]
    [InlineData(GpuUsageSources.Windows, "unavailable")]
    [InlineData(GpuUsageSources.Nvidia, "available")]
    [InlineData(GpuUsageSources.Nvidia, "partial")]
    [InlineData(GpuUsageSources.Nvidia, "unavailable")]
    [InlineData(GpuUsageSources.Amd, "available")]
    [InlineData(GpuUsageSources.Amd, "partial")]
    [InlineData(GpuUsageSources.Amd, "unavailable")]
    public void SupportedGraphicsSourcesPreserveTheCompleteSummary(string source, string status)
    {
        var telemetry = Collected(partial: status == "partial");
        var metric = status == "unavailable"
            ? ResourceSummary.Summarize([], 120, "percent", source, "whole_adapter", "counter_unavailable")
            : telemetry.Gpu.GraphicsUtilization with { Source = source };
        telemetry = telemetry with { Gpu = telemetry.Gpu with { GraphicsUtilization = metric },
            Status = status == "unavailable" ? "partial" : telemetry.Status,
            Warnings = telemetry.Warnings.Concat(metric.ReasonCodes).Distinct().Order().ToArray() };
        var run = Run(telemetry);
        var json = SubmissionPayload.Create(run, GpuName);
        SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
        var projected = JsonNode.Parse(json)!;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(telemetry, JsonDefaults.Options), projected["resource_telemetry"]));
        Assert.Equal(source, projected["resource_telemetry"]!["gpu"]!["graphics_utilization"]!["source"]!.GetValue<string>());
        Assert.Equal("pdh_gpu_adapter_memory_dedicated", telemetry.Gpu.DedicatedMemoryUsed.Source);
        Assert.Equal("pdh_gpu_adapter_memory_shared", telemetry.Gpu.SharedMemoryUsed.Source);
        Export("graphics-" + source + "-" + status, json);
    }

    [Theory]
    [InlineData(GpuUsageSources.Windows, true)]
    [InlineData(GpuUsageSources.Nvidia, false)]
    [InlineData(GpuUsageSources.Amd, false)]
    public void SupportedDurationUsesTheActualGraphicsSource(string source, bool accepted)
    {
        var telemetry = Collected();
        telemetry = telemetry with { Gpu = telemetry.Gpu with { GraphicsUtilization =
            telemetry.Gpu.GraphicsUtilization with { Source = source, ValidSampleCount = 80 } } };
        var run = Run(telemetry);
        if (accepted)
        {
            var json = SubmissionPayload.Create(run, GpuName);
            SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
            Export("graphics-support-" + source, json);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => SubmissionPayload.Create(run, GpuName));
            var json = JsonNode.Parse(SubmissionPayload.Create(Run(Collected()), GpuName))!;
            json["resource_telemetry"]!["gpu"]!["graphics_utilization"]!["source"] = source;
            json["resource_telemetry"]!["gpu"]!["graphics_utilization"]!["valid_sample_count"] = 80;
            Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json.ToJsonString(), Guid.Parse(run.RunId)));
            Export("invalid-graphics-support-" + source, json.ToJsonString());
        }
    }

    [Theory]
    [InlineData("processors")]
    [InlineData("files")]
    [InlineData("duplicate-processors")]
    [InlineData("duplicate-files")]
    [InlineData("duplicate-reasons")]
    public void ExcessiveArraysAndDuplicateIndexesOrReasonsFail(string scenario)
    {
        var run = Run(Collected());
        var dto = JsonNode.Parse(SubmissionPayload.Create(run, GpuName))!;
        var resource = dto["resource_telemetry"]!;
        var array = scenario.Contains("processors") ? resource["cpu"]!["logical_processors"]!.AsArray() :
            scenario.Contains("files") ? resource["pagefile"]!["files"]!.AsArray() : resource["warnings"]!.AsArray();
        if (scenario == "duplicate-reasons") { array.Add("counter_unavailable"); array.Add("counter_unavailable"); }
        else
        {
            var length = scenario.StartsWith("duplicate") ? array.Count + 1 : scenario == "processors" ? 513 : 33;
            while (array.Count < length) array.Add(array[0]!.DeepClone());
        }
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(dto.ToJsonString(), Guid.Parse(run.RunId)));
        Export("invalid-" + scenario, dto.ToJsonString());
    }

    [Fact]
    public void DuplicateJsonKeysAreNotLostInFrozenReprojection()
    {
        var run = Run(Collected());
        var json = SubmissionPayload.Create(run, GpuName).Replace("\"target_interval_sec\":1", "\"target_interval_sec\":1,\"target_interval_sec\":1");
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId)));
    }

    [Fact]
    public void NonFiniteCollectorValuesCannotCrossTheUploadBoundary()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity })
        {
            var telemetry = Collected();
            telemetry = telemetry with { Cpu = telemetry.Cpu with { TotalUtilization = telemetry.Cpu.TotalUtilization with { Average = value } } };
            Assert.Throws<InvalidDataException>(() => SubmissionPayload.Create(Run(telemetry), GpuName));
        }
    }

    [Theory]
    [InlineData(.9999986, "partial")]
    [InlineData(.999999, "available")]
    [InlineData(.9999994, "available")]
    public void IndependentlyRoundedCoverageUsesBackendTolerance(double coverage, string expected)
    {
        var metric = ResourceSummary.Summarize([(25, coverage * 120)], 120, "percent", "pdh_processor_information_processor_time", "whole_system", "counter_unavailable")
            with { ValidSampleCount = 120 };
        var telemetry = Collected() with { Cpu = Collected().Cpu with { TotalUtilization = metric }, Status = expected,
            Warnings = metric.ReasonCodes };
        var run = Run(telemetry);
        var json = SubmissionPayload.Create(run, GpuName);
        SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
        Export("coverage-" + coverage.ToString(System.Globalization.CultureInfo.InvariantCulture), json);
    }

    private static (JsonObject Parent, string Key) At(JsonNode root, string path)
    {
        var keys = path.Split('.');
        var node = root;
        foreach (var key in keys[..^1]) node = node is JsonArray array ? array[int.Parse(key)]! : node[key]!;
        return (node.AsObject(), keys[^1]);
    }
    internal static void Export(string scenario, string json)
    {
        var directory = Environment.GetEnvironmentVariable("ACADEMY_RESOURCE_CONTRACT_OUTPUT");
        if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, scenario + ".json"), json); }
    }
}
