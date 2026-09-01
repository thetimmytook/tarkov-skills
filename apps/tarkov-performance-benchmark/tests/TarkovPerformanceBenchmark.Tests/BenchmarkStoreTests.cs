using System.Text.Json;
using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class BenchmarkStoreTests
{
    [Fact]
    public void FirstAppendCreatesMissingDataDirectoryAndDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "TarkovBenchmarkTests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "nested", "benchmark.json");
        try
        {
            var store = new BenchmarkStore(path);
            store.Append(CreateRun("run-1"));

            Assert.True(File.Exists(path));
            var document = store.Load();
            Assert.Single(document.Runs);
            Assert.Equal("run-1", document.Runs[0].RunId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MarkSubmittedUpdatesOnlySelectedRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "TarkovBenchmarkTests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "benchmark.json");
        try
        {
            var store = new BenchmarkStore(path);
            store.Append(CreateRun("run-1"));
            store.Append(CreateRun("run-2"));
            store.MarkSubmitted(["run-2"]);

            var document = store.Load();
            Assert.False(document.Runs[0].Submitted);
            Assert.True(document.Runs[1].Submitted);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SubmissionPayloadContainsOnlySelectedRuns()
    {
        var json = BenchmarkSubmission.Serialize([CreateRun("run-2")]);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("schema_version").GetInt32());
        var runs = document.RootElement.GetProperty("runs");
        Assert.Equal(1, runs.GetArrayLength());
        Assert.Equal("run-2", runs[0].GetProperty("run_id").GetString());
        Assert.DoesNotContain(@"C:\Users\", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmissionSelectsOnlyTwentyMostRecentUnsubmittedRuns()
    {
        var runs = Enumerable.Range(1, 25).Select(index => CreateRun($"run-{index:D2}")).ToList();

        var selected = BenchmarkSubmission.SelectUnsubmitted(runs);

        Assert.Equal(20, selected.Count);
        Assert.Equal("run-06", selected[0].RunId);
        Assert.Equal("run-25", selected[^1].RunId);
    }

    private static BenchmarkRun CreateRun(string id) => new(
        id,
        "2026-09-01",
        120,
        "1.0.1",
        new { cpu = "test" },
        new { graphics = new { quality = "test" } },
        new { map = "Factory", execution = "bsg" },
        new PerformanceMetrics(1200, 120, 10, 8, 5, 100, 120, 160),
        []);
}
