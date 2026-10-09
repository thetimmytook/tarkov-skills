namespace TarkovSkills.Core;

// Read-only Windows boundaries are bound by FrametimeCaptureService. Keeping the
// lifetime here gives GUI and CLI one completion/discard rule and permits testing
// raid/game events without running, reading or manipulating a game process.
internal sealed record CaptureOperations(
    Func<CancellationToken, Task<FrameCapture>> Frames,
    Func<CaptureWindow, Task<ResourceTelemetry>> Resources,
    Func<RaidContext> Context, Func<bool> GameExited, Func<bool> RaidEnded);
internal sealed record CapturedPerformance(PerformanceMetrics Performance, ResourceTelemetry ResourceTelemetry, RaidContext Context);

internal static class CaptureLifecycle
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(4);

    public static async Task<CapturedPerformance> RunAsync(int duration, CaptureOperations operations, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var raidEnded = 0;
        var monitor = MonitorAsync();
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (operations.GameExited()) throw new CaptureDiscardedException("Tarkov closed before capture started. No data was saved.");
            var initialContext = operations.Context();
            if (!initialContext.Active) throw new CaptureDiscardedException("The raid ended before capture started. No data was saved.");
            var capture = await operations.Frames(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            Validate(capture);
            ResourceTelemetry telemetry;
            try { telemetry = await operations.Resources(capture.Window).ConfigureAwait(false); }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
            catch { telemetry = ResourceTelemetry.Unavailable(duration, "collector_unavailable"); }
            linked.Token.ThrowIfCancellationRequested();
            var completedContext = Validate(capture);
            if (operations.RaidEnded() || completedContext.StartedAt != initialContext.StartedAt)
                throw new CaptureDiscardedException("The raid ended before capture completed. Partial data was discarded.");
            linked.Token.ThrowIfCancellationRequested();
            return new(capture.Metrics, telemetry, completedContext);
        }
        catch (OperationCanceledException) when (Volatile.Read(ref raidEnded) == 1)
        {
            throw new CaptureDiscardedException("The raid ended before capture completed. Partial data was discarded.");
        }
        catch (Exception) when (operations.GameExited())
        {
            throw new CaptureDiscardedException("Tarkov closed before capture completed. Partial data was discarded.");
        }
        finally
        {
            linked.Cancel();
            try { await monitor.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }

        RaidContext Validate(FrameCapture capture)
        {
            var context = operations.Context();
            CaptureValidation.EnsureComplete(capture.Metrics, context, operations.GameExited(), duration);
            return context;
        }

        async Task MonitorAsync()
        {
            while (!linked.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, linked.Token).ConfigureAwait(false);
                if (operations.GameExited()) { linked.Cancel(); return; }
                try
                {
                    if (operations.RaidEnded())
                    {
                        Interlocked.Exchange(ref raidEnded, 1);
                        linked.Cancel();
                        return;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
