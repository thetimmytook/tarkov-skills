using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core;

public static class CaptureDiagnostics
{
    public static bool Enabled
    {
        get
        {
#if CAPTURE_DIAGNOSTICS
            return true;
#else
            return false;
#endif
        }
    }
    // Capture logging and the standalone permissions experiment are independent.
    // A normal capture-test build can retain logs without enabling the probe.
    public static bool PermissionProbeEnabled
    {
        get
        {
#if CAPTURE_DIAGNOSTICS && CAPTURE_PERMISSION_PROBE
            return true;
#else
            return false;
#endif
        }
    }
    public const string BuildLabel = "AMD Tarkov capture 2026-10-18.3";

    internal static CaptureDiagnosticLog? Start(int seconds) =>
        CaptureDiagnosticLog.Create(AppPaths.ReportsDirectory, seconds);
    internal static CaptureDiagnosticLog? StartPermissionProbe() =>
        PermissionProbeEnabled ? CaptureDiagnosticLog.Create(AppPaths.ReportsDirectory, 3, true) : null;

    internal static async Task<T> ObserveAsync<T>(CaptureDiagnosticLog? log, Func<Task<T>> operation)
    {
        try
        {
            var result = await operation().ConfigureAwait(false);
            log?.Finish(null);
            return result;
        }
        catch (Exception error)
        {
            log?.Finish(error);
            throw;
        }
    }
}

internal enum CaptureDiagnosticStage
{
    inspection_started, inspection_completed, resources_starting, resources_ready,
    presentmon_preparing, presentmon_started, presentmon_cleanup_started, presentmon_cleanup_completed,
    frames_validated
}

// Closed diagnostic events: never serialize an exception, raw CSV row, command line,
// process/device identity, unrecognized stdout/stderr text or a local path. Logging is best effort.
internal sealed class CaptureDiagnosticLog : IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private readonly Action<string, object>? _relay;

    private CaptureDiagnosticLog(StreamWriter? writer) => _writer = writer;
    private CaptureDiagnosticLog(Action<string, object> relay) => _relay = relay;
    internal static CaptureDiagnosticLog Relay(Action<string, object> relay) => new(relay);
    internal void FromHelper(CaptureRelayMessage message)
    {
        var clean = CaptureRelayPrivacy.Sanitize(message);
        if (clean is not null) Write("elevated_" + message.Stage, clean.Value);
    }

    internal static CaptureDiagnosticLog Create(string directory, int seconds, bool permissionProbe = false)
    {
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(directory);
            DiagnosticRetention.Trim(directory, "capture-*.log", 19);
            var name = $"{(permissionProbe ? "permission-probe" : "capture")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log";
            writer = new StreamWriter(new FileStream(Path.Combine(directory, name), FileMode.CreateNew,
                FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception) { /* Diagnostic storage must never invalidate FPS. */ }
        var log = new CaptureDiagnosticLog(writer);
        log.Write("started", new { diagnostic_build = CaptureDiagnostics.Enabled ? CaptureDiagnostics.BuildLabel : null, requested_seconds = seconds,
            presentmon_version = "2.5.1", windows_version = Environment.OSVersion.Version.ToString() });
        return log;
    }

    internal void Stage(CaptureDiagnosticStage stage) => Write(stage.ToString(), new { });
    internal void HelperFailure(CaptureFailure? failure, int? windowsError) => Write("elevated_collector_failed", new
    {
        reason = failure.HasValue && Enum.IsDefined(failure.Value) ? failure.Value.ToString() : "Collector",
        windows_error = windowsError
    });

    internal void PresentMonExited(int? exitCode, double seconds, bool cancelled, string stdout, string stderr) =>
        ProcessExited(CaptureProcessRole.capture, exitCode, seconds, cancelled, false,
            PresentMonOutputSummary.FromText(stdout), PresentMonOutputSummary.FromText(stderr));

    internal void ProcessExited(CaptureProcessRole role, int? exitCode, double seconds, bool cancelled, bool timedOut,
        PresentMonOutputSummary stdout, PresentMonOutputSummary stderr) =>
        Write(role == CaptureProcessRole.capture ? "presentmon_exited" : "presentmon_cleanup_exited",
            new { role = role.ToString(), exit_code = exitCode, process_elapsed_seconds = Math.Round(seconds, 6),
                cancellation_requested = cancelled, timed_out = timedOut,
                stdout_characters = stdout.Characters, stderr_characters = stderr.Characters,
                stdout_summary = stdout, stderr_summary = stderr,
                access_denied_reported = stdout.AccessDeniedReported || stderr.AccessDeniedReported,
                session_conflict_reported = stdout.SessionConflictReported || stderr.SessionConflictReported,
                lost_events_reported = stdout.LostEventsReported || stderr.LostEventsReported,
                lost_buffers_reported = stdout.LostBuffersReported || stderr.LostBuffersReported,
                warning_reported = stdout.WarningReported || stderr.WarningReported });

    internal void ProcessOutput(CaptureProcessRole role, CaptureOutputStream stream, long lineNumber,
        long characters, bool truncated, PresentMonOutputMessage message) =>
        Write("presentmon_output", new { role = role.ToString(), stream = stream.ToString(), line_number = lineNumber,
            characters, truncated, message_code = message.Code, severity = message.Severity, message = message.Text });

    internal void OutputLimit(CaptureProcessRole role, CaptureOutputStream stream, int limit) =>
        Write("presentmon_output_limit", new { role = role.ToString(), stream = stream.ToString(), max_logged_lines = limit });

    internal void CsvSummary(CaptureParseDiagnostics summary) => Write("csv_summary", summary.Snapshot());

    internal void Frames(FrameCapture capture) => Write("frames_received", new {
        sample_count = capture.Metrics.SampleCount, valid_duration_seconds = capture.Metrics.DurationSec,
        aligned_duration_seconds = capture.Window.Duration, interval_count = capture.Window.Intervals.Count });

    internal void Resources(ResourceTelemetry telemetry) => Write("resources_completed", new {
        status = telemetry.Status, gpu_source = telemetry.Gpu.GraphicsUtilization.Source,
        gpu_status = telemetry.Gpu.GraphicsUtilization.Status,
        gpu_valid_samples = telemetry.Gpu.GraphicsUtilization.ValidSampleCount,
        gpu_coverage = telemetry.Gpu.GraphicsUtilization.Coverage });

    internal void Finish(Exception? error)
    {
        var outcome = error switch { null => "completed", CaptureDiscardedException => "discarded",
            OperationCanceledException => "cancelled", _ => "failed" };
        // Messages are inspected only for known local failures, never persisted.
        var reason = error switch
        {
            null => "capture_complete",
            CaptureDiscardedException e when e.Message.StartsWith("Only ", StringComparison.Ordinal) => "insufficient_valid_duration",
            CaptureDiscardedException e when e.Message.Contains("Tarkov closed", StringComparison.Ordinal) => "game_closed",
            CaptureDiscardedException => "raid_ended",
            OperationCanceledException => "cancelled",
            PresentMonPermissionException => "trace_permission_required",
            PresentMonSessionException => "trace_session_conflict",
            InvalidDataException e when e.Message.Contains("too few frame", StringComparison.Ordinal) => "too_few_frames",
            InvalidDataException => "invalid_capture_data",
            _ => "capture_failed"
        };
        Write("finished", new { outcome, reason, error_type = error?.GetType().Name });
    }

    private void Write(string stage, object data)
    {
        lock (_gate)
        {
            if (_relay is not null) { try { _relay(stage, data); } catch { } return; }
            if (_writer is null) return;
            try { _writer.WriteLine(JsonSerializer.Serialize(new { schema_version = 1, utc = DateTimeOffset.UtcNow,
                elapsed_seconds = Math.Round(_clock.Elapsed.TotalSeconds, 6), stage, data })); }
            catch (Exception) { try { _writer.Dispose(); } catch { } _writer = null; }
        }
    }

    public void Dispose()
    {
        lock (_gate) { try { _writer?.Dispose(); } catch { } _writer = null; }
    }
}
