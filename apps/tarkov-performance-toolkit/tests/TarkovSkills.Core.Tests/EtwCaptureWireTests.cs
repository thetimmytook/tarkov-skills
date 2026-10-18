using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwCaptureWireTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8388609)]
    public async Task UntrustedPacketLengthIsRejectedBeforeAllocatingPayload(int size)
    {
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, size);
        using var stream = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() => EtwCaptureWire.ReadAsync(stream, default));
    }

    [Fact]
    public async Task ArbitraryCommandsPathsAndUnknownPropertiesCannotEnterTheCollector()
    {
        var payload = Encoding.UTF8.GetBytes("{\"Kind\":1,\"Duration\":120,\"Executable\":\"evil.exe\"}");
        using var stream = new MemoryStream(); var length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        stream.Write(length); stream.Write(payload); stream.Position = 0;
        await Assert.ThrowsAsync<JsonException>(() => EtwCaptureWire.ReadAsync(stream, default));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateCommand(new(CapturePacketKind.Start, 30)));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateCommand(new(CapturePacketKind.Cancel, 120)));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateCommand(new(CapturePacketKind.Result)));
    }

    [Fact]
    public async Task PrivateFrameWindowRoundTripsWithoutLosingQpcAlignment()
    {
        var expected = new CaptureFrameMessage(new(7200, 120, 60, 50, 40, 16.667, 20, 25), [new(12345, 12465)]);
        using var stream = new MemoryStream(EtwCaptureWire.Encode(new(CapturePacketKind.Result, Frame: expected)));
        var packet = await EtwCaptureWire.ReadAsync(stream, default);
        var frames = EtwCaptureWire.ValidateFrame(packet.Frame, 120);
        Assert.Equal(expected.Metrics, frames.Metrics);
        Assert.Equal(12345, frames.Window.Intervals[0].Start);
        Assert.Equal(120, frames.Window.Duration);
    }

    [Fact]
    public void NonFiniteOverlappingOrOversizedWindowsAreRejected()
    {
        var metrics = new PerformanceMetrics(7200, 120, 60, 50, 40, 16.667, 20, 25);
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateFrame(new(metrics, [new(10, 15), new(14, 16)]), 120));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateFrame(new(metrics, [new(double.NaN, 15)]), 120));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateFrame(new(metrics with { AverageFps = double.PositiveInfinity }, []), 120));
        Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateFrame(new(metrics with { DurationSec = 10000 }, []), 120));
    }

    [Fact]
    public void RelayedDiagnosticsDropUnknownKeysAndAllFreeTextBeforePersistence()
    {
        var data = JsonSerializer.SerializeToElement(new { role = "capture", stream = "stderr", line_number = 3,
            message_code = "trace_access_denied", message = @"C:\Users\private-name\secret", PID = 1234,
            severity = "error", private_name = "do not persist", characters = 88 });
        var clean = CaptureRelayPrivacy.Sanitize(new("presentmon_output", data))!.Value.GetRawText();
        Assert.Contains("trace_access_denied", clean);
        foreach (var forbidden in new[] { "private-name", "secret", "PID", "private_name", "do not persist" }) Assert.DoesNotContain(forbidden, clean);
        Assert.Null(CaptureRelayPrivacy.Sanitize(new("private-session-name", data)));
        var forged = CaptureRelayPrivacy.Sanitize(new("presentmon_output", JsonSerializer.SerializeToElement(new { message_code = "user_identity_secret" })))!.Value;
        Assert.Equal(JsonValueKind.Null, forged.GetProperty("message_code").ValueKind);
    }

    [Fact]
    public void DiagnosticProbeNeverRequestsAnArbitraryProcessOutputOrElevationSwitch()
    {
        var arguments = PresentMonRunner.BuildProbeArguments();
        Assert.Contains("--no_csv", arguments);
        Assert.Contains("--timed", arguments); Assert.Contains("3", arguments);
        Assert.DoesNotContain("--output_file", arguments); Assert.DoesNotContain("--output_stdout", arguments);
        Assert.DoesNotContain("--restart_as_admin", arguments);
        Assert.Equal("EscapeFromTarkov.exe", arguments[arguments.ToList().IndexOf("--process_name") + 1]);
        if (!CaptureDiagnostics.PermissionProbeEnabled) Assert.Throws<InvalidDataException>(() => EtwCaptureWire.ValidateCommand(new(CapturePacketKind.Probe)));
    }

    [Fact]
    public void CaptureHelperArgumentsHaveNoCallerSelectedTargetAndRoundTrip()
    {
        var args = new[] { "--capture-helper", "--pipe", Guid.NewGuid().ToString("N") };
        Assert.True(EtwPermissionRequest.TryParse(args, out var parsed));
        Assert.True(parsed!.CaptureSession); Assert.Equal(args, parsed.Arguments);
        Assert.True(EtwPermissions.IsHelperInvocation(args));
        Assert.False(EtwPermissionRequest.TryParse([.. args, "--exe", "anything.exe"], out _));
        Assert.False(EtwPermissionRequest.TryParse(["--capture-helper", "--pipe", "\\remote\\pipe\\test"], out _));
    }
}
