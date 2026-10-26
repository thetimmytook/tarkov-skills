using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TarkovSkills.Core.Permissions;

internal enum CapturePacketKind { Start = 1, Cancel, Started, Result, Cancelled, Failed, Diagnostic, Ready, Probe, ProbeResult }
internal enum CaptureFailure { Permission = 1, Conflict, InvalidData, Dependency, Collector }
internal sealed record CaptureFrameMessage(PerformanceMetrics Metrics, CaptureInterval[] Intervals);
internal sealed record CaptureRelayMessage(string Stage, JsonElement Data);
internal sealed record CapturePacket(CapturePacketKind Kind, int? Duration = null,
    CaptureFrameMessage? Frame = null, CaptureFailure? Failure = null, CaptureRelayMessage? Log = null, int? WindowsError = null);

// Private local IPC only. No caller-supplied executable, arguments, output path,
// account, game configuration or benchmark history crosses this boundary.
internal static class EtwCaptureWire
{
    internal const int MaximumPacketBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static async Task<CapturePacket> ReadAsync(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellation).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumPacketBytes) throw new InvalidDataException("Invalid collector packet size.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellation).ConfigureAwait(false);
        var packet = JsonSerializer.Deserialize<CapturePacket>(bytes, Options);
        if (packet is null || !Enum.IsDefined(packet.Kind)) throw new InvalidDataException("Invalid collector packet.");
        return packet;
    }

    internal static byte[] Encode(CapturePacket packet)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(packet, Options);
        if (payload.Length > MaximumPacketBytes) throw new InvalidDataException("Collector packet is too large.");
        var bytes = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    internal static void ValidateCommand(CapturePacket packet)
    {
        if (packet.Frame is not null || packet.Failure is not null || packet.Log is not null || packet.WindowsError is not null
            || packet.Kind == CapturePacketKind.Start && packet.Duration is not (120 or 240)
            || packet.Kind == CapturePacketKind.Cancel && packet.Duration is not null
            || packet.Kind == CapturePacketKind.Probe && (packet.Duration is not null || !CaptureDiagnostics.PermissionProbeEnabled)
            || packet.Kind is not (CapturePacketKind.Start or CapturePacketKind.Cancel or CapturePacketKind.Probe))
            throw new InvalidDataException("Invalid collector command.");
    }

    internal static FrameCapture ValidateFrame(CaptureFrameMessage? frame, int requested)
    {
        if (frame?.Metrics is not { } metrics || frame.Intervals is null || frame.Intervals.Length > 100000
            || metrics.SampleCount is < 120 or > 10000000)
            throw new InvalidDataException("Invalid collector result.");
        var values = new[] { metrics.DurationSec, metrics.AverageFps, metrics.OnePercentLowFps,
            metrics.ZeroPointOnePercentLowFps, metrics.AverageFrametimeMs, metrics.P95FrametimeMs, metrics.P99FrametimeMs };
        if (values.Any(value => !double.IsFinite(value) || value <= 0) || metrics.DurationSec > requested + 30)
            throw new InvalidDataException("Invalid collector metrics.");
        var end = 0d;
        foreach (var interval in frame.Intervals)
        {
            if (!double.IsFinite(interval.Start) || !double.IsFinite(interval.End) || interval.Start < end
                || interval.Start < 0 || interval.End <= interval.Start)
                throw new InvalidDataException("Invalid collector window.");
            end = interval.End;
        }
        // QPC is machine-wide, so the normal host's sampler uses these same instants.
        return new(metrics, new(frame.Intervals));
    }

    internal static Exception FailureException(CaptureFailure? failure) => failure switch
    {
        CaptureFailure.Permission => new PresentMonPermissionException("Windows denied access to the elevated performance collector."),
        CaptureFailure.Conflict => new PresentMonSessionException("Another performance capture is running. Stop it and try again."),
        CaptureFailure.InvalidData => new InvalidDataException("The performance collector returned incomplete or invalid frame data."),
        CaptureFailure.Dependency => new InvalidOperationException("The bundled performance collector failed its integrity check."),
        _ => new InvalidOperationException("The temporary performance collector could not complete the measurement.")
    };
}

internal sealed class CapturePacketWriter(Stream stream)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal async Task WriteAsync(CapturePacket packet, CancellationToken cancellation)
    {
        var bytes = EtwCaptureWire.Encode(packet);
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try { await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    // Diagnostic callbacks are synchronous and bounded; the peer continuously drains.
    internal void Write(CapturePacket packet)
    {
        var bytes = EtwCaptureWire.Encode(packet);
        _gate.Wait();
        try { stream.Write(bytes); }
        finally { _gate.Release(); }
    }
}
