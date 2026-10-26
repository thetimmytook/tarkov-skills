namespace TarkovSkills.Core.Permissions;

internal sealed record CaptureAccessLaunch(EtwPermissionResult Result, IEtwCaptureConnection? Connection);

// GUI-owned lifetime. CLI uses FrametimeCaptureService directly and never opts
// into interactive approval or elevation. Every retry creates fresh inspection,
// raid validation and resource sampling after permission setup has completed.
public sealed class CaptureAccessSession : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _ownership = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Func<IEtwCaptureConnection?, int, Action, CancellationToken, Task<CaptureReport>> _capture;
    private readonly Func<CancellationToken, Task<CaptureAccessLaunch>> _launch;
    private readonly Func<EtwPermissionState> _check;
    private readonly Action<EtwPermissionResult> _report;
    private IEtwCaptureConnection? _connection;

    public CaptureAccessSession() : this(
        (connection, duration, started, cancellation) => new FrametimeCaptureService().CaptureUsingAsync(duration, started, cancellation,
            connection is null ? null : (seconds, onStarted, log, token) => connection.CaptureAsync(seconds, onStarted, log, token)),
        EtwPermissionTransport.LaunchCaptureAsync, EtwPermissions.CheckCurrent,
        result => EtwPermissionReport.Save(EtwPermissionAction.Add, result)) { }

    internal CaptureAccessSession(Func<IEtwCaptureConnection?, int, Action, CancellationToken, Task<CaptureReport>> capture,
        Func<CancellationToken, Task<CaptureAccessLaunch>> launch, Func<EtwPermissionState> check, Action<EtwPermissionResult> report)
    { _capture = capture; _launch = launch; _check = check; _report = report; }

    public bool TemporaryCollectorActive => _connection?.IsConnected == true && !_lifetime.IsCancellationRequested;

    public Task<CaptureReport> CaptureAsync(int duration, Action started,
        Func<CancellationToken, Task<bool>> approveAccess, CancellationToken cancellation) =>
        RunWithRecoveryAsync((connection, token) => _capture(connection, duration, started, token), approveAccess, cancellation);

    public async Task TestCaptureAccessAsync(Func<CancellationToken, Task<bool>> approveAccess, CancellationToken cancellation)
    {
        if (!CaptureDiagnostics.PermissionProbeEnabled) throw new NotSupportedException("The permission probe is not enabled in this build.");
        using var diagnostic = CaptureDiagnostics.StartPermissionProbe();
        await CaptureDiagnostics.ObserveAsync(diagnostic, () => RunWithRecoveryAsync(async (connection, token) =>
        {
            if (connection is null) await new PresentMonRunner().ProbeAsync(diagnostic, token).ConfigureAwait(false);
            else await connection.ProbeAsync(diagnostic, token).ConfigureAwait(false);
            return true;
        }, approveAccess, cancellation)).ConfigureAwait(false);
    }

    private async Task<T> RunWithRecoveryAsync<T>(Func<IEtwCaptureConnection?, CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<bool>> approveAccess, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_connection?.IsConnected == false) StopTemporaryCollector();
            try { return await operation(_connection, linked.Token).ConfigureAwait(false); }
            catch (PresentMonPermissionException) when (_connection is null)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (!await approveAccess(linked.Token).ConfigureAwait(false)) throw new OperationCanceledException(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                var result = await EnableCoreAsync(true, linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                if (result.Outcome == EtwPermissionOutcome.Cancelled) throw new OperationCanceledException(linked.Token);
                if (!TemporaryCollectorActive) throw new PresentMonPermissionException(result.Message);
                return await operation(_connection, linked.Token).ConfigureAwait(false);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<EtwPermissionResult> EnableAsync(CancellationToken cancellation = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try { return await EnableCoreAsync(false, linked.Token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<EtwPermissionResult> EnableCoreAsync(bool captureDenied, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_connection?.IsConnected == false) StopTemporaryCollector();
        if (_connection is not null || !captureDenied && _check().CurrentTokenHasAccess) return new(EtwPermissionOutcome.Available);
        var launched = await _launch(cancellation).ConfigureAwait(false);
        try { _report(launched.Result); } catch { }
        lock (_ownership)
        {
            if (cancellation.IsCancellationRequested) { launched.Connection?.Dispose(); cancellation.ThrowIfCancellationRequested(); }
            _connection = launched.Connection;
        }
        return launched.Result;
    }

    // Settings revocation first releases the temporary elevated collector.
    public void StopTemporaryCollector()
    {
        IEtwCaptureConnection? connection;
        lock (_ownership) connection = Interlocked.Exchange(ref _connection, null);
        connection?.Dispose();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        StopTemporaryCollector();
    }
}
