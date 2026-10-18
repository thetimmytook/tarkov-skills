using System.Text.Json;
using System.ComponentModel;

namespace TarkovSkills.Core.Permissions;

internal interface IEtwCaptureConnection : IDisposable
{
    bool IsConnected { get; }
    Task<FrameCapture> CaptureAsync(int duration, Action started, CaptureDiagnosticLog? diagnostic, CancellationToken cancellation);
    Task ProbeAsync(CaptureDiagnosticLog? diagnostic, CancellationToken cancellation);
}

internal sealed class EtwCaptureConnection(Stream pipe) : IEtwCaptureConnection
{
    private readonly CapturePacketWriter _writer = new(pipe);
    private bool _disposed;
    public bool IsConnected => !_disposed;

    public async Task<FrameCapture> CaptureAsync(int duration, Action started, CaptureDiagnosticLog? diagnostic, CancellationToken cancellation) =>
        await RunAsync(new(CapturePacketKind.Start, duration), duration, started, diagnostic, cancellation).ConfigureAwait(false)
        ?? throw new InvalidDataException("The collector returned no frame data.");

    public async Task ProbeAsync(CaptureDiagnosticLog? diagnostic, CancellationToken cancellation)
    {
        if (!CaptureDiagnostics.PermissionProbeEnabled) throw new NotSupportedException("The permission probe is not enabled in this build.");
        await RunAsync(new(CapturePacketKind.Probe), 3, () => { }, diagnostic, cancellation).ConfigureAwait(false);
    }

    private async Task<FrameCapture?> RunAsync(CapturePacket command, int duration, Action started, CaptureDiagnosticLog? diagnostic, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EtwCaptureWire.ValidateCommand(command);
        cancellation.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(duration + 30));
        var collectorFailure = false;
        Task cancellationWrite = Task.CompletedTask;
        CancellationTokenRegistration registration = default;
        try
        {
            await _writer.WriteAsync(command, deadline.Token).ConfigureAwait(false);
            registration = cancellation.Register(() =>
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(12));
                cancellationWrite = SendCancellationAsync(deadline.Token);
            });
            var hasStarted = false;
            while (true)
            {
                var packet = await EtwCaptureWire.ReadAsync(pipe, deadline.Token).ConfigureAwait(false);
                switch (packet.Kind)
                {
                    case CapturePacketKind.Started when !hasStarted:
                        hasStarted = true;
                        if (!cancellation.IsCancellationRequested) started();
                        break;
                    case CapturePacketKind.Diagnostic when packet.Log is not null:
                        diagnostic?.FromHelper(packet.Log);
                        break;
                    case CapturePacketKind.Result when hasStarted && command.Kind == CapturePacketKind.Start:
                        cancellation.ThrowIfCancellationRequested();
                        return EtwCaptureWire.ValidateFrame(packet.Frame, duration);
                    case CapturePacketKind.ProbeResult when command.Kind == CapturePacketKind.Probe:
                        cancellation.ThrowIfCancellationRequested();
                        return null;
                    case CapturePacketKind.Cancelled:
                        throw new OperationCanceledException(cancellation);
                    case CapturePacketKind.Failed:
                        cancellation.ThrowIfCancellationRequested();
                        diagnostic?.HelperFailure(packet.Failure, packet.WindowsError);
                        collectorFailure = true;
                        throw EtwCaptureWire.FailureException(packet.Failure);
                    default: throw new InvalidDataException("Unexpected collector response.");
                }
            }
        }
        catch (Exception error) when ((error is IOException or JsonException or InvalidDataException) && !collectorFailure || deadline.IsCancellationRequested)
        {
            Dispose();
            cancellation.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The temporary performance collector disconnected. Start a new measurement to retry.");
        }
        finally
        {
            // Do not let a delayed Cancel command affect the next measurement.
            registration.Dispose();
            await cancellationWrite.ConfigureAwait(false);
        }
    }

    private async Task SendCancellationAsync(CancellationToken cancellation)
    {
        try { await _writer.WriteAsync(new(CapturePacketKind.Cancel), cancellation).ConfigureAwait(false); }
        catch (Exception) { /* Closing the pipe also stops the helper's active capture. */ }
    }

    public void Dispose() { _disposed = true; pipe.Dispose(); }
}

internal static class EtwCaptureBroker
{
    internal static async Task RunAsync(Stream pipe, Func<int, Action, CaptureDiagnosticLog?, CancellationToken, Task<FrameCapture>> capture,
        Func<CaptureDiagnosticLog?, CancellationToken, Task>? probe = null)
    {
        using var lifetime = new CancellationTokenSource();
        var writer = new CapturePacketWriter(pipe);
        CancellationTokenSource? activeCancellation = null;
        Task<CapturePacket>? active = null;
        var read = EtwCaptureWire.ReadAsync(pipe, lifetime.Token);
        try
        {
            while (true)
            {
                if (active is not null && await Task.WhenAny(active, read).ConfigureAwait(false) == active)
                {
                    var result = await active.ConfigureAwait(false);
                    active = null; activeCancellation!.Dispose(); activeCancellation = null;
                    await writer.WriteAsync(result, lifetime.Token).ConfigureAwait(false);
                    continue;
                }
                var command = await read.ConfigureAwait(false);
                EtwCaptureWire.ValidateCommand(command);
                if (command.Kind == CapturePacketKind.Cancel) activeCancellation?.Cancel();
                else
                {
                    if (active is not null) throw new InvalidDataException("A capture is already active.");
                    activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    active = CaptureAsync(command, activeCancellation.Token);
                }
                read = EtwCaptureWire.ReadAsync(pipe, lifetime.Token);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException or JsonException) { }
        finally
        {
            lifetime.Cancel(); activeCancellation?.Cancel();
            if (active is not null) { try { await active.WaitAsync(TimeSpan.FromSeconds(12)).ConfigureAwait(false); } catch { } }
            try { await read.ConfigureAwait(false); } catch { }
            activeCancellation?.Dispose();
        }

        async Task<CapturePacket> CaptureAsync(CapturePacket command, CancellationToken cancellation)
        {
            using var diagnostic = CaptureDiagnosticLog.Relay((stage, data) => writer.Write(new(CapturePacketKind.Diagnostic,
                Log: new(stage, JsonSerializer.SerializeToElement(data)))));
            try
            {
                if (command.Kind == CapturePacketKind.Probe)
                {
                    if (probe is null) throw new InvalidDataException("The permission probe is unavailable.");
                    await probe(diagnostic, cancellation).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    return new(CapturePacketKind.ProbeResult);
                }
                var frames = await capture(command.Duration!.Value, () => writer.Write(new(CapturePacketKind.Started)), diagnostic, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                return new(CapturePacketKind.Result, Frame: new(frames.Metrics, frames.Window.Intervals.ToArray()));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            { return new(CapturePacketKind.Cancelled); }
            catch (Exception error)
            {
                var reason = error switch
                {
                    PresentMonPermissionException => CaptureFailure.Permission,
                    PresentMonSessionException => CaptureFailure.Conflict,
                    InvalidDataException => CaptureFailure.InvalidData,
                    FileNotFoundException => CaptureFailure.Dependency,
                    _ => CaptureFailure.Collector
                };
                return new(CapturePacketKind.Failed, Failure: reason,
                    WindowsError: error is Win32Exception native ? native.NativeErrorCode : null);
            }
        }
    }
}
