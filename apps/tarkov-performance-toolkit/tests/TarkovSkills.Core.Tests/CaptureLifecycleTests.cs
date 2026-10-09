namespace TarkovSkills.Core.Tests;

public sealed class CaptureLifecycleTests
{
    private static readonly RaidContext Active = new(true, true, "Woods", "woods", null, DateTime.Now, null);
    private static readonly FrameCapture Frames = new(new(7200, 120, 60, 50, 40, 16.666, 20, 25), CaptureWindow.FromFrames([new(10, 130)]));

    private sealed class Sources
    {
        public bool Exited, Ended;
        public int CaptureCalls, SummaryCalls, ContextCalls;
        public Func<CancellationToken, Task<FrameCapture>> Capture = _ => Task.FromResult(Frames);
        public Func<CaptureWindow, Task<ResourceTelemetry>> Summary = _ => Task.FromResult(ResourceTelemetry.Unavailable(120, "counter_unavailable"));
        public Func<RaidContext>? ReadContext;
        public CaptureOperations Bind() => new(
            token => { CaptureCalls++; return Capture(token); },
            window => { SummaryCalls++; return Summary(window); },
            () => { ContextCalls++; return ReadContext?.Invoke() ?? (Ended ? Active with { Active = false } : Active); },
            () => Exited, () => Ended);
    }

    [Fact]
    public async Task UnavailableAndFailedTelemetryPreserveCompleteFpsAndAlignedWindow()
    {
        foreach (var failure in new[] { false, true })
        {
            var sources = new Sources();
            sources.Summary = window =>
            {
                Assert.Same(Frames.Window, window);
                if (failure) throw new InvalidOperationException(@"native failure C:\Users\private\capture.csv");
                return Task.FromResult(ResourceTelemetry.Unavailable(120, "counter_unavailable"));
            };
            var complete = await CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None);
            Assert.Same(Frames.Metrics, complete.Performance);
            Assert.Equal("unavailable", complete.ResourceTelemetry.Status);
            Assert.Equal(1, sources.SummaryCalls);
            Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(complete.ResourceTelemetry));
        }
    }

    [Fact]
    public async Task CancellationBeforeStartDoesNotCaptureOrSummarize()
    {
        var sources = new Sources();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), cancel.Token));
        Assert.Equal(0, sources.CaptureCalls);
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GameOrRaidAlreadyEndedNeverStartsCapture(bool game)
    {
        var sources = new Sources { Exited = game, Ended = !game };
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None));
        Assert.Equal(0, sources.CaptureCalls);
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Fact]
    public async Task UserCancellationInterruptsCaptureAndDoesNotSummarizePartialFrames()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sources = new Sources { Capture = async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return Frames; } };
        using var cancellation = new CancellationTokenSource();
        var capture = CaptureLifecycle.RunAsync(120, sources.Bind(), cancellation.Token);
        await started.Task; cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MonitorCancelsWhenGameClosesOrRaidEndsDuringCapture(bool game)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sources = new Sources { Capture = async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return Frames; } };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var capture = CaptureLifecycle.RunAsync(120, sources.Bind(), timeout.Token);
        await started.Task;
        if (game) sources.Exited = true; else sources.Ended = true;
        var discarded = await Assert.ThrowsAsync<CaptureDiscardedException>(() => capture);
        Assert.Contains(game ? "Tarkov closed" : "raid ended", discarded.Message);
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExitImmediatelyAfterFrameCompletionDiscardsBeforeResourceSummary(bool game)
    {
        var sources = new Sources();
        sources.Capture = _ => { if (game) sources.Exited = true; else sources.Ended = true; return Task.FromResult(Frames); };
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None));
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("game")]
    [InlineData("raid")]
    public async Task CompletionRechecksLifetimeAfterResourceSummary(string action)
    {
        using var cancel = new CancellationTokenSource();
        var sources = new Sources();
        sources.Summary = _ =>
        {
            if (action == "cancel") cancel.Cancel();
            if (action == "game") sources.Exited = true;
            if (action == "raid") sources.Ended = true;
            return Task.FromResult(ResourceTelemetry.Unavailable(120, "counter_unavailable"));
        };
        if (action == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), cancel.Token));
        else await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), cancel.Token));
        Assert.Equal(1, sources.SummaryCalls);
    }

    [Fact]
    public async Task ContextFailureDoesNotProduceCompletedRun()
    {
        var sources = new Sources { ReadContext = () => throw new IOException("Log unavailable.") };
        await Assert.ThrowsAsync<IOException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None));
        Assert.Equal(0, sources.CaptureCalls);
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Fact]
    public async Task ProviderReturningFramesAfterCancellationCannotComplete()
    {
        using var cancel = new CancellationTokenSource();
        var sources = new Sources { Capture = _ => { cancel.Cancel(); return Task.FromResult(Frames); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), cancel.Token));
        Assert.Equal(0, sources.SummaryCalls);
    }

    [Fact]
    public async Task IndependentTelemetryCancellationIsUnavailableAndPreservesFps()
    {
        var sources = new Sources { Summary = _ => throw new OperationCanceledException() };
        var result = await CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None);
        Assert.Equal("unavailable", result.ResourceTelemetry.Status);
        Assert.Equal(Frames.Metrics, result.Performance);
    }

    [Fact]
    public async Task ShortFramesAndPresentMonFailuresDoNotCreateTelemetryOrSuccess()
    {
        var shortRun = new Sources { Capture = _ => Task.FromResult(Frames with { Metrics = Frames.Metrics with { DurationSec = 60 } }) };
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureLifecycle.RunAsync(120, shortRun.Bind(), CancellationToken.None));
        Assert.Equal(0, shortRun.SummaryCalls);
        var failed = new Sources { Capture = _ => throw new PresentMonPermissionException("Windows denied trace access.") };
        await Assert.ThrowsAsync<PresentMonPermissionException>(() => CaptureLifecycle.RunAsync(120, failed.Bind(), CancellationToken.None));
        Assert.Equal(0, failed.SummaryCalls);
    }

    [Fact]
    public async Task CompletionReturnsTheValidatedContextAndDoesNotReadLiveStateAfterwards()
    {
        var current = Active;
        var sources = new Sources { ReadContext = () => current };
        sources.Summary = _ =>
        {
            current = Active with { GameVersion = "version-at-completion" };
            return Task.FromResult(ResourceTelemetry.Unavailable(120, "counter_unavailable"));
        };
        var result = await CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None);
        var reads = sources.ContextCalls;
        sources.Exited = true;
        sources.Ended = true;
        current = Active with { Map = "Customs", Active = false, GameVersion = "later-version" };
        Assert.Equal("version-at-completion", result.Context.GameVersion);
        Assert.Equal("Woods", result.Context.Map);
        Assert.True(result.Context.Active);
        Assert.Equal(reads, sources.ContextCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RaidTransitionDuringSummaryCannotBecomeACompletedRun(bool endMarker)
    {
        var current = Active;
        var sources = new Sources { ReadContext = () => current };
        sources.Summary = _ =>
        {
            if (endMarker) sources.Ended = true;
            else current = Active with { StartedAt = Active.StartedAt!.Value.AddMinutes(5) };
            return Task.FromResult(ResourceTelemetry.Unavailable(120, "counter_unavailable"));
        };
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureLifecycle.RunAsync(120, sources.Bind(), CancellationToken.None));
    }
}
