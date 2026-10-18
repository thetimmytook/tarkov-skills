using System.Text.Json;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core.Permissions;

// Revalidate diagnostics crossing IPC before persistence. All string fields have
// closed vocabularies; human-readable collector output is omitted in relayed logs.
internal static class CaptureRelayPrivacy
{
    private static readonly HashSet<string> Stages = ["presentmon_preparing", "presentmon_started",
        "presentmon_cleanup_started", "presentmon_cleanup_completed", "presentmon_exited", "presentmon_cleanup_exited",
        "presentmon_output", "presentmon_output_limit", "csv_summary"];
    private static readonly HashSet<string> Fields = ["role", "stream", "line_number", "characters", "truncated", "message_code",
        "severity", "message", "max_logged_lines", "exit_code", "process_elapsed_seconds", "cancellation_requested", "timed_out",
        "stdout_characters", "stderr_characters", "stdout_summary", "stderr_summary", "access_denied_reported", "session_conflict_reported",
        "lost_events_reported", "lost_buffers_reported", "warning_reported", "Characters", "Lines", "UnknownLines", "ReadCompleted",
        "ReadFailed", "LimitReached", "WarningReported", "AccessDeniedReported", "SessionConflictReported", "LostEventsReported", "LostBuffersReported",
        "csv_bytes", "ends_with_newline", "known_columns", "selected_frametime_column", "header_column_count", "rows_read", "valid_frames",
        "rejected_frames", "rejected_frame_reasons", "missing_value", "non_numeric_or_non_finite", "non_positive", "at_least_10_seconds",
        "largest_finite_frametime_ms", "rejected_long_frametime_sum_seconds", "valid_frame_window_seconds", "valid_frame_window_gap_count",
        "valid_frame_window_gap_seconds", "valid_frame_window_largest_gap_seconds", "malformed_csv_rows", "column_count_mismatches",
        "frametime_sum_seconds", "relative_timestamp_span_seconds", "qpc_timestamp_span_seconds", "valid_qpc_rows", "qpc_alignment_available",
        "application_column_present", "tarkov_application_rows", "unknown_application_rows", "other_application_rows"];
    private static readonly HashSet<string> Columns = ["MsBetweenPresents", "FrameTime", "CPUFrameTime", "MsBetweenDisplayChange",
        "TimeInQPC", "QPCTime", "CPUStartQPC", "TimeInMs", "CPUStartTimeInMs", "unavailable", "other_frametime"];
    private static readonly HashSet<string> ArchiveStages = ["started", "inspection_started", "inspection_completed",
        "resources_starting", "resources_ready", "frames_received", "frames_validated", "resources_completed", "finished",
        "elevated_collector_failed", "windows_capture_access"];
    private static readonly HashSet<string> ArchiveFields = ["diagnostic_build", "requested_seconds", "presentmon_version", "windows_version",
        "sample_count", "valid_duration_seconds", "aligned_duration_seconds", "interval_count", "status", "gpu_source", "gpu_status",
        "gpu_valid_samples", "gpu_coverage", "outcome", "reason", "windows_error", "schema_version", "utc", "action"];

    internal static JsonElement? Sanitize(CaptureRelayMessage message) => Sanitize(message, false);
    internal static JsonElement? SanitizeForArchive(CaptureRelayMessage message) => Sanitize(message, true);

    private static JsonElement? Sanitize(CaptureRelayMessage message, bool archive)
    {
        var stage = archive && message.Stage.StartsWith("elevated_", StringComparison.Ordinal) ? message.Stage[9..] : message.Stage;
        if (!(Stages.Contains(stage) || archive && ArchiveStages.Contains(message.Stage)) || message.Data.ValueKind != JsonValueKind.Object) return null;
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) Copy(writer, message.Data, "", 0, archive);
        using var document = JsonDocument.Parse(bytes.ToArray());
        return document.RootElement.Clone();
    }

    private static void Copy(Utf8JsonWriter writer, JsonElement value, string field, int depth, bool archive)
    {
        if (depth > 4) { writer.WriteNullValue(); return; }
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().Take(100))
                {
                    if (!Fields.Contains(property.Name) && !(archive && ArchiveFields.Contains(property.Name))) continue;
                    writer.WritePropertyName(property.Name); Copy(writer, property.Value, property.Name, depth + 1, archive);
                }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array when field == "known_columns":
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray().Take(12))
                    if (item.ValueKind == JsonValueKind.String && Columns.Contains(item.GetString()!)) writer.WriteStringValue(item.GetString());
                writer.WriteEndArray(); break;
            case JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number): writer.WriteNumberValue(number); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                var allowed = field switch
                {
                    "role" => Enum.GetNames<CaptureProcessRole>().Contains(text),
                    "stream" => Enum.GetNames<CaptureOutputStream>().Contains(text),
                    "severity" => text is "info" or "warning" or "error" or "unknown",
                    "message_code" => PresentMonOutputMessages.IsKnownCode(text),
                    "selected_frametime_column" => Columns.Contains(text),
                    _ => false
                };
                if (allowed || archive && ArchiveString(field, text)) writer.WriteStringValue(text); else writer.WriteNullValue();
                break;
            default: writer.WriteNullValue(); break;
        }
    }

    private static bool ArchiveString(string field, string value) => field switch
    {
        "diagnostic_build" => Regex.IsMatch(value, @"\AAMD (capture diagnostics|Tarkov capture) [0-9]{4}-[0-9]{2}-[0-9]{2}\.[0-9]{1,4}\z"),
        "presentmon_version" or "windows_version" => Version.TryParse(value, out _),
        "utc" => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _) && value.Length <= 40,
        "status" or "gpu_status" => value is "available" or "partial" or "unavailable" or "not_collected",
        "gpu_source" => value is "pdh_gpu_engine_3d_busiest_engine" or "nvapi_gpu_graphics_utilization" or "adlx_gpu_usage",
        "action" => Enum.GetNames<EtwPermissionAction>().Contains(value),
        "outcome" => value is "completed" or "discarded" or "cancelled" or "failed" || Enum.GetNames<EtwPermissionOutcome>().Contains(value),
        "reason" => value is "capture_complete" or "insufficient_valid_duration" or "game_closed" or "raid_ended" or "cancelled"
            or "trace_permission_required" or "trace_session_conflict" or "too_few_frames" or "invalid_capture_data" or "capture_failed"
            || Enum.GetNames<CaptureFailure>().Contains(value),
        _ => false
    };
}
