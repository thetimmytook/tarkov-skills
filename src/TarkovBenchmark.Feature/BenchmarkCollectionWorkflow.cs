using System.Globalization;
using TarkovSkills.Core;

namespace TarkovBenchmark.Feature;

internal sealed record BenchmarkCollectionOperations(
    Func<RaidContext> Context, Func<bool> GameExited,
    Func<CancellationToken, Task<CaptureReport>> Capture,
    Func<RaidContext, Task<ContextAnswers?>> Review, Action<BenchmarkRun> Save);
internal sealed record SavedBenchmark(BenchmarkRun Run, string Map);

// Shared by both hosts; native capture and modal review stay at the UI boundary.
internal static class BenchmarkCollectionWorkflow
{
    internal static async Task<SavedBenchmark?> RunAsync(int duration, string version,
        BenchmarkCollectionOperations operations, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var initialContext = operations.Context();
        if (operations.GameExited() || !initialContext.Active)
            throw new CaptureDiscardedException("Enter a raid before starting capture.");
        var measurement = await operations.Capture(cancellation);
        // CaptureAsync returns only after the shared Core lifecycle has validated
        // completion. Live game/raid state must never invalidate this snapshot.
        var completedContext = measurement.Inspection.Raid;
        var answers = await operations.Review(completedContext);
        if (answers is null) return null;
        cancellation.ThrowIfCancellationRequested();
        var run = new BenchmarkRun(Guid.NewGuid().ToString(), measurement.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), duration, version,
            measurement.Inspection.System, measurement.Inspection.Settings,
            new { map = answers.Map, execution = answers.Execution, weather = answers.Weather,
                time_of_day = answers.TimeOfDay, game_version = completedContext.GameVersion },
            measurement.Performance, measurement.Inspection.Warnings.ToList())
        { ResourceTelemetry = measurement.ResourceTelemetry };
        operations.Save(run);
        return new(run, answers.Map);
    }
}
