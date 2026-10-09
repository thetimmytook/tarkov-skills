using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Tests.Authentication;

namespace TarkovSkills.Core.Tests;

public sealed class ResourceCaptureContractTests
{
    [Theory]
    [InlineData("TimeInQPC,MsBetweenPresents", false)]
    [InlineData("QPCTime,MsBetweenPresents", false)]
    [InlineData("CPUStartQPC,FrameTime", true)]
    public void ValidFrameWindowUsesCorrectQpcEdgeWithoutChangingFps(string header, bool forward)
    {
        WithCsv(header, Enumerable.Range(1, 120).Select(i =>
            $"{(long)((100 + i / 10d) * Stopwatch.Frequency)},100"), path =>
        {
            var capture = PresentMonCsvParser.ParseCapture(path);
            Assert.Equal(120, capture.Metrics.SampleCount);
            Assert.Equal(10, capture.Metrics.AverageFps);
            Assert.Equal(12, capture.Window.Duration, 6);
            Assert.Equal(forward ? 100.1 : 100, capture.Window.Intervals[0].Start, 6);
            Assert.Equal(forward ? 112.1 : 112, capture.Window.Intervals[^1].End, 6);
        });
    }

    [Fact]
    public void PresentTimesAreNotReplacedWithCpuStartTimes()
    {
        WithCsv("TimeInQPC,MsBetweenPresents,CPUStartQPC", Enumerable.Range(1, 120).Select(i =>
            $"{(long)((100 + i / 10d) * Stopwatch.Frequency)},100,{Stopwatch.Frequency}"), path =>
            Assert.Equal(100, PresentMonCsvParser.ParseCapture(path).Window.Intervals[0].Start, 6));
    }

    [Theory]
    [InlineData("MsBetweenPresents", "100")]
    [InlineData("TimeInQPC,MsBetweenPresents", "NA,100")]
    [InlineData("CPUStartQPC,MsBetweenPresents", "100,100")]
    public void MissingOrMismatchedQpcLeavesSuccessfulFpsWithUnavailableAlignment(string header, string row)
    {
        WithCsv(header, Enumerable.Repeat(row, 120), path =>
        {
            var capture = PresentMonCsvParser.ParseCapture(path);
            Assert.Equal(12, capture.Metrics.DurationSec);
            Assert.Empty(capture.Window.Intervals);
        });
    }

    [Fact]
    public void InvalidFramesCreateUncoveredIntervalsRatherThanAContinuousEnvelope()
    {
        var rows = Enumerable.Range(1, 120).Select(i =>
            $"{(long)((100 + i / 10d + (i > 60 ? 20 : 0)) * Stopwatch.Frequency)},100");
        WithCsv("TimeInQPC,MsBetweenPresents", rows, path =>
        {
            var window = PresentMonCsvParser.ParseCapture(path).Window;
            Assert.Equal(12, window.Duration, 6);
            Assert.Equal(2, window.Intervals.Count);
            Assert.Equal(0, window.GaugeSupport(110));
        });
    }

    [Fact]
    public void MandatorySummaryPersistsCopiesAndOldRunsOpenAsNotCollected()
    {
        var path = Path.Combine(Path.GetTempPath(), "telemetry-history-" + Guid.NewGuid(), "benchmark.json");
        try
        {
            var run = SubmissionTests.Run() with { ResourceTelemetry = ResourceTelemetry.Unavailable(120, "collector_unavailable") };
            var store = new BenchmarkStore(path);
            store.Append(run);
            var restored = Assert.Single(store.Load().Runs);
            Assert.Equal("unavailable", restored.ResourceTelemetry.Status);
            using var copied = JsonDocument.Parse(BenchmarkSubmission.SerializeLatest([restored]));
            Assert.Equal("unavailable", copied.RootElement.GetProperty("runs")[0].GetProperty("resource_telemetry").GetProperty("status").GetString());
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            legacy["runs"]![0]!.AsObject().Remove("resource_telemetry");
            File.WriteAllText(path, legacy.ToJsonString());
            Assert.Equal("not_collected", Assert.Single(store.Load().Runs).ResourceTelemetry.Status);
            legacy["runs"]![0]!["resource_telemetry"] = null;
            File.WriteAllText(path, legacy.ToJsonString());
            Assert.Equal("not_collected", Assert.Single(store.Load().Runs).ResourceTelemetry.Status);
            using var command = JsonDocument.Parse(JsonSerializer.Serialize(new CommandResult("completed"), JsonDefaults.Options));
            Assert.True(command.RootElement.TryGetProperty("resource_telemetry", out _));
            using var discarded = JsonDocument.Parse(JsonSerializer.Serialize(new CommandResult("discarded"), JsonDefaults.Options));
            Assert.False(discarded.RootElement.TryGetProperty("resource_telemetry", out _));
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void TelemetryIsIncludedOnlyInSubmissionAndNeverInCohortCriteria()
    {
        var run = SubmissionTests.Run();
        var baseline = SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070");
        var enriched = run with { ResourceTelemetry = ResourceTelemetry.Unavailable(120, "collector_unavailable") };
        Assert.NotEqual(baseline, SubmissionPayload.Create(enriched, "NVIDIA GeForce RTX 4070"));
        Assert.Contains("resource_telemetry", baseline);
        SubmissionPayload.ValidateStored(baseline, Guid.Parse(run.RunId));
        Assert.Equal(BenchmarkComparison.Query(run, "NVIDIA GeForce RTX 4070"),
            BenchmarkComparison.Query(enriched, "NVIDIA GeForce RTX 4070"));
    }

    [Theory]
    [InlineData(120, 120, false)]
    [InlineData(240, 120, true)]
    [InlineData(240, 229, true)]
    [InlineData(240, 240, false)]
    public void CompletenessUsesRequestedCaptureDuration(int requested, double measured, bool rejected)
    {
        var raid = new RaidContext(true, true, "Woods", "woods", null, DateTime.Now, null);
        var metrics = new PerformanceMetrics(12000, measured, 100, 80, 60, 10, 15, 20);
        if (rejected) Assert.Throws<CaptureDiscardedException>(() => CaptureValidation.EnsureComplete(metrics, raid, false, requested));
        else CaptureValidation.EnsureComplete(metrics, raid, false, requested);
    }

    private static void WithCsv(string header, IEnumerable<string> rows, Action<string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), "telemetry-frames-" + Guid.NewGuid() + ".csv");
        try { File.WriteAllLines(path, new[] { header }.Concat(rows)); check(path); }
        finally { File.Delete(path); }
    }
}
