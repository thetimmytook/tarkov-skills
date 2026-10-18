using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class CaptureAccessSessionTests
{
    [Fact]
    public async Task SuccessfulOrdinaryCaptureNeverChecksPermissionOrLaunchesUac()
    {
        using var session = Session((connection, _, started, _) =>
        {
            Assert.Null(connection); started(); return Task.FromResult(Report());
        }, _ => throw new Exception("Must not elevate"));
        var starts = 0;
        Assert.Same(ReportValue, await session.CaptureAsync(120, () => starts++, _ => throw new Exception("Must not prompt"), default));
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task DenialWaitsForApprovalThenRestartsCaptureAndReusesTheHelper()
    {
        var events = new List<string>();
        var connection = new FakeConnection();
        using var session = Session((actual, _, started, _) =>
        {
            events.Add(actual is null ? "ordinary" : "fresh-sampling");
            if (actual is null) throw new PresentMonPermissionException("denied");
            Assert.Same(connection, actual); started(); return Task.FromResult(Report());
        }, _ => { events.Add("uac"); return Task.FromResult(new CaptureAccessLaunch(new(EtwPermissionOutcome.Added), connection)); });
        await session.CaptureAsync(120, () => events.Add("timer"), _ => { events.Add("consent"); return Task.FromResult(true); }, default);
        await session.CaptureAsync(120, () => events.Add("timer"), _ => throw new Exception("Must reuse helper"), default);
        Assert.Equal(new[] { "ordinary", "consent", "uac", "fresh-sampling", "timer", "fresh-sampling", "timer" }, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclinedConsentAndCancelledUacNeverStartAnElevatedCapture(bool consent)
    {
        var attempts = 0; var launches = 0;
        using var session = Session((_, _, _, _) => { attempts++; throw new PresentMonPermissionException("denied"); },
            _ => { launches++; return Task.FromResult(new CaptureAccessLaunch(new(EtwPermissionOutcome.Cancelled), null)); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.CaptureAsync(120, () => throw new Exception("No timer"),
            _ => Task.FromResult(consent), default));
        Assert.Equal(1, attempts); Assert.Equal(consent ? 1 : 0, launches);
    }

    [Fact]
    public async Task GameOrRaidExitDuringApprovalIsRecheckedByFreshCapture()
    {
        var ended = false;
        using var session = Session((connection, _, _, _) => connection is null
            ? throw new PresentMonPermissionException("denied")
            : ended ? throw new CaptureDiscardedException("raid ended") : Task.FromResult(Report()),
            _ => Task.FromResult(new CaptureAccessLaunch(new(EtwPermissionOutcome.Added), new FakeConnection())));
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => session.CaptureAsync(120, () => throw new Exception("Must not start"),
            _ => { ended = true; return Task.FromResult(true); }, default));
    }

    [Fact]
    public async Task ClosingTheSessionDuringUacDisposesTheReturnedCollectorWithoutRetry()
    {
        var connection = new FakeConnection();
        var launch = new TaskCompletionSource<CaptureAccessLaunch>();
        var entered = new TaskCompletionSource();
        using var session = Session((_, _, _, _) => throw new PresentMonPermissionException("denied"),
            _ => { entered.SetResult(); return launch.Task; });
        var capture = session.CaptureAsync(120, () => { }, _ => Task.FromResult(true), default);
        await entered.Task; session.Dispose();
        launch.SetResult(new(new(EtwPermissionOutcome.Added), connection));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        Assert.False(connection.IsConnected);
        Assert.False(session.TemporaryCollectorActive);
    }

    [Fact]
    public async Task PendingGroupMembershipStillStartsATemporaryCollectorFromSettings()
    {
        var calls = 0; var connection = new FakeConnection();
        using var session = new CaptureAccessSession((_, _, _, _) => throw new Exception("No capture"),
            _ => { calls++; return Task.FromResult(new CaptureAccessLaunch(new(EtwPermissionOutcome.AlreadyMember), connection)); },
            () => new(false, true, false), _ => { });
        Assert.Equal(EtwPermissionOutcome.AlreadyMember, (await session.EnableAsync()).Outcome);
        Assert.True(session.TemporaryCollectorActive);
        await session.EnableAsync(); Assert.Equal(1, calls);
        session.StopTemporaryCollector(); Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task PreparationFailureIsNotMisdiagnosedAsPermissionDenial()
    {
        using var session = Session((_, _, _, _) => throw new InvalidDataException("capture invalid"), _ => throw new Exception("Must not elevate"));
        await Assert.ThrowsAsync<InvalidDataException>(() => session.CaptureAsync(120, () => { }, _ => throw new Exception("Must not prompt"), default));
    }

    [Fact]
    public async Task ExpiredCollectorIsReleasedBeforeAnotherOrdinaryAttempt()
    {
        var connection = new FakeConnection(); var ordinary = 0;
        using var session = new CaptureAccessSession((actual, _, _, _) => { Assert.Null(actual); ordinary++; return Task.FromResult(Report()); },
            _ => Task.FromResult(new CaptureAccessLaunch(new(EtwPermissionOutcome.Added), connection)), () => new(false, false, false), _ => { });
        await session.EnableAsync(); connection.Dispose();
        await session.CaptureAsync(120, () => { }, _ => throw new Exception("Must not prompt"), default);
        Assert.Equal(1, ordinary);
    }

    private static CaptureAccessSession Session(Func<IEtwCaptureConnection?, int, Action, CancellationToken, Task<CaptureReport>> capture,
        Func<CancellationToken, Task<CaptureAccessLaunch>> launch) => new(capture, launch,
            () => throw new Exception("Ordinary capture does not need a preliminary permission check"), _ => { });

    private static readonly CaptureReport ReportValue = new(1, DateTimeOffset.UnixEpoch,
        new(1, DateTimeOffset.UnixEpoch, true, new(true, true, "Factory", "Local", null, null, null),
            GoalStore.Default(), new { }, new { }, []), new(7200, 120, 60, 50, 40, 16.667, 20, 25), ResourceTelemetry.NotCollected);
    private static CaptureReport Report() => ReportValue;

    internal sealed class FakeConnection : IEtwCaptureConnection
    {
        public bool IsConnected { get; private set; } = true;
        public Task<FrameCapture> CaptureAsync(int duration, Action started, CaptureDiagnosticLog? diagnostic, CancellationToken cancellation) =>
            throw new NotSupportedException();
        public Task ProbeAsync(CaptureDiagnosticLog? diagnostic, CancellationToken cancellation) => throw new NotSupportedException();
        public void Dispose() => IsConnected = false;
    }
}
