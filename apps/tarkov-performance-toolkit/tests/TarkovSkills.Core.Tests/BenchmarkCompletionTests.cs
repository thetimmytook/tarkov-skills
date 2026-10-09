using System.Text.Json;
using TarkovBenchmark.Feature;

namespace TarkovSkills.Core.Tests;

public sealed class BenchmarkCompletionTests
{
    private static readonly RaidContext Raid = new(true, true, "Woods", "woods", "capture-version", new(2026, 10, 9, 12, 0, 0), null);
    private static readonly PerformanceMetrics Performance = new(6000, 120, 50, 40, 30, 20, 25, 35);
    private static readonly CaptureReport Measurement = new(1, DateTimeOffset.Now,
        new(1, DateTimeOffset.Now, true, Raid, GoalStore.Default(), new { cpu = "fixture" }, new { graphics = "fixture" }, []),
        Performance, ResourceTelemetry.Unavailable(120, "counter_unavailable"));

    [Theory]
    [InlineData(false, "game")]
    [InlineData(false, "raid")]
    [InlineData(false, "next-raid")]
    [InlineData(true, "game")]
    [InlineData(true, "raid")]
    [InlineData(true, "next-raid")]
    public async Task CompletedMeasurementSavesAfterExitDuringReviewAndKeepsCapturedContext(bool toolkit, string action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "benchmark-completion-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BenchmarkStore(Path.Combine(directory, "benchmark.json"));
            var current = Raid;
            var exited = false;
            var options = toolkit ? BenchmarkFeatureOptions.ForToolkit("toolkit-test") :
                BenchmarkFeatureOptions.ForStandalone("benchmark-test", false, false, null);
            var operations = new BenchmarkCollectionOperations(() => current, () => exited,
                _ => Task.FromResult(Measurement), context =>
                {
                    Assert.Equal(Raid, context);
                    exited = action == "game";
                    current = action == "next-raid" ? Raid with { Map = "Customs", GameVersion = "later-version", StartedAt = Raid.StartedAt!.Value.AddMinutes(5) } :
                        Raid with { Active = false, EndedAt = Raid.StartedAt!.Value.AddMinutes(3) };
                    return Task.FromResult<ContextAnswers?>(new("local", "clear", "day", context.Map));
                }, store.Append);

            var saved = await BenchmarkCollectionWorkflow.RunAsync(120, options.ApplicationVersion, operations, CancellationToken.None);

            Assert.NotNull(saved);
            var persisted = Assert.Single(store.Load().Runs);
            Assert.Equal(saved.Run.RunId, persisted.RunId);
            Assert.Equal(Performance, persisted.Performance);
            Assert.Equal(options.ApplicationVersion, persisted.AppVersion);
            var context = JsonSerializer.SerializeToElement(persisted.Context);
            Assert.Equal("Woods", context.GetProperty("map").GetString());
            Assert.Equal("capture-version", context.GetProperty("game_version").GetString());
            Assert.Equal("unavailable", persisted.ResourceTelemetry.Status);
            Assert.False(persisted.Submitted);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("game")]
    [InlineData("raid")]
    public async Task ValidatedCaptureIsNotInvalidatedBeforeReviewOpens(string action)
    {
        var exited = false;
        var current = Raid;
        var saved = new List<BenchmarkRun>();
        var operations = new BenchmarkCollectionOperations(() => current, () => exited,
            _ =>
            {
                exited = action == "game";
                current = Raid with { Active = false };
                return Task.FromResult(Measurement); // Capture service has already validated completion.
            }, context => Task.FromResult<ContextAnswers?>(new("local", "unknown", "unknown", context.Map)), saved.Add);
        await BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, CancellationToken.None);
        Assert.Single(saved);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("decline")]
    public async Task UserCanStillDiscardCompletedMeasurementWithoutSaving(string action)
    {
        using var cancellation = new CancellationTokenSource();
        var saved = new List<BenchmarkRun>();
        var operations = new BenchmarkCollectionOperations(() => Raid, () => false,
            _ => Task.FromResult(Measurement), _ =>
            {
                if (action == "cancel") cancellation.Cancel();
                return Task.FromResult<ContextAnswers?>(action == "decline" ? null : new("local", "unknown", "unknown", "Woods"));
            }, saved.Add);
        if (action == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, cancellation.Token));
        else Assert.Null(await BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, cancellation.Token));
        Assert.Empty(saved);
    }

    [Theory]
    [InlineData("game")]
    [InlineData("raid")]
    [InlineData("cancel")]
    public async Task UnfinishedCaptureNeverOpensReviewOrSaves(string action)
    {
        using var cancellation = new CancellationTokenSource();
        var reviews = 0;
        var saved = new List<BenchmarkRun>();
        var operations = new BenchmarkCollectionOperations(() => Raid, () => false,
            _ =>
            {
                if (action == "cancel") { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
                throw new CaptureDiscardedException(action + " ended during capture.");
            }, _ => { reviews++; return Task.FromResult<ContextAnswers?>(new("local", "clear", "day", "Woods")); }, saved.Add);
        if (action == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, cancellation.Token));
        else await Assert.ThrowsAsync<CaptureDiscardedException>(() => BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, cancellation.Token));
        Assert.Equal(0, reviews);
        Assert.Empty(saved);
    }

    [Fact]
    public async Task ReviewAfterMidnightKeepsCaptureDateAndSaveErrorsAreNotMisreportedAsRaidExit()
    {
        var measurement = Measurement with { GeneratedAt = new DateTimeOffset(2026, 10, 9, 23, 59, 59, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 9))) };
        BenchmarkRun? saved = null;
        var exited = false;
        var operations = new BenchmarkCollectionOperations(() => Raid, () => exited,
            _ => Task.FromResult(measurement), _ =>
            {
                exited = true;
                return Task.FromResult<ContextAnswers?>(new("local", "unknown", "unknown", "Woods"));
            }, run => saved = run);
        await BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, CancellationToken.None);
        Assert.Equal("2026-10-09", saved!.CollectedDate);
        exited = false;
        operations = operations with { Save = _ => throw new IOException("fixture save failed") };
        await Assert.ThrowsAsync<IOException>(() => BenchmarkCollectionWorkflow.RunAsync(120, "test", operations, CancellationToken.None));
    }
}
