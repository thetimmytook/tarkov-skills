using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core;

internal enum CaptureProcessRole { capture, prepare_cleanup, final_cleanup, legacy_cleanup, cleanup_fallback }
internal enum CaptureOutputStream { stdout, stderr }

internal sealed record PresentMonOutputMessage(string Code, string Severity, string? Text);

// Only fixed messages from the pinned 2.5.1 source and parsed numeric counters may
// leave this boundary. Unknown lines are counted, never copied or partially redacted.
internal static class PresentMonOutputMessages
{
    internal static bool IsKnownCode(string code) => Fixed.Values.Contains(code) || code is
        "etw_events_lost" or "etw_buffers_lost" or "present_events_overflowed" or "trace_access_denied" or "trace_start_failed"
        or "trace_session_absent" or "trace_stop_failed" or "trace_session_replaced" or "trace_session_conflict"
        or "embedded_manifest_failed" or "unrecognized_output";
    private static readonly IReadOnlyDictionary<string, string> Fixed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Started recording."] = "recording_started",
        ["Stopped recording."] = "recording_stopped",
        ["warning: PresentMon requires elevated privilege in order to query processes that are"] = "process_query_privilege_limited",
        ["short-running or started on another account.  Without it, those processes will"] = "process_query_privilege_detail",
        ["be listed as '<unknown>' and they can't be targeted by --process_name nor trigger"] = "process_query_targeting_limited",
        ["--terminate_on_proc_exit."] = "process_query_exit_detection_limited",
        ["PresentMon requires either administrative privileges or to be run by a user in the"] = "trace_permission_required",
        ["\"Performance Log Users\" user group.  View the readme for more details."] = "trace_permission_group",
        ["Use --session_name with a different name to start a new session."] = "trace_session_name_hint",
        ["to stop the existing session, or use --session_name with a different name to"] = "trace_session_stop_hint",
        ["start a new session."] = "trace_session_start_hint",
        ["warning: could not toggle scroll lock."] = "scroll_lock_failed",
        ["error: failed to get exe fullpath"] = "executable_path_failed",
        ["error: failed to register hotkey class."] = "hotkey_class_failed",
        ["error: failed to create hotkey window."] = "hotkey_window_failed",
        ["Consider increasing the present event circular buffer size to a value larger"] = "present_buffer_size_hint",
        ["than the default of 2048, e.g., --set_circular_buffer_size 4096."] = "present_buffer_size_default",
        ["warning: --no_console_stats added because stdout does not support statistics reporting."] = "console_statistics_unavailable",
        ["warning: --terminate_existing_session exits without capturing anything; ignoring all other options."] = "cleanup_ignored_capture_options",
        ["warning: ignoring --track_gpu_video due to --no_track_gpu."] = "gpu_video_tracking_ignored",
        ["warning: ignoring --no_track_display because display tracking is required when GPU tracking is enabled."] = "display_tracking_required",
        ["warning: ignoring --track_frame_type due to --v1_metrics."] = "frame_type_tracking_ignored",
        ["warning: ignoring --v1_metrics due to --v2_metrics."] = "v1_metrics_ignored",
        ["warning: ignoring --multi_csv due to --output_stdout."] = "multi_csv_ignored",
        ["error: --set_circular_buffer_size must be a power of 2."] = "invalid_present_buffer_size"
    };

    internal static PresentMonOutputMessage Classify(string line)
    {
        var text = line.Trim();
        var severity = text.StartsWith("warning:", StringComparison.OrdinalIgnoreCase) ? "warning"
            : text.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ? "error" : "info";
        if (Fixed.TryGetValue(text, out var code)) return new(code, severity, text);

        var lost = Match(text, @"\Awarning: ([0-9]{1,10}) ETW (events|buffers) were lost\.\z");
        if (lost.Success && uint.TryParse(lost.Groups[1].Value, CultureInfo.InvariantCulture, out var count))
            return new(lost.Groups[2].Value == "events" ? "etw_events_lost" : "etw_buffers_lost", "warning",
                $"warning: {count.ToString(CultureInfo.InvariantCulture)} ETW {lost.Groups[2].Value} were lost.");

        var overflow = Match(text, @"\Awarning: ([0-9]{1,10}) overflowed present events detected\. This could be due to a high-fps application\.\z");
        if (overflow.Success && uint.TryParse(overflow.Groups[1].Value, CultureInfo.InvariantCulture, out count))
            return new("present_events_overflowed", "warning", $"warning: {count.ToString(CultureInfo.InvariantCulture)} overflowed present events detected.");

        var failure = Match(text, @"\Aerror: failed to start trace session: (file not found\.|path not found\.|invalid --session_name\.|access denied\.|invalid --etl_file\.|error code [0-9]{1,10}\.)\z");
        if (failure.Success) return new(failure.Groups[1].Value == "access denied." ? "trace_access_denied" : "trace_start_failed", "error", text);

        // The variable session name and executable path are deliberately discarded.
        if (Match(text, @"\Aerror: no existing sessions found: .+\z").Success)
            return new("trace_session_absent", "error", "error: no existing sessions found: [session omitted]");
        var stop = Match(text, @"\Aerror: failed to terminate existing session \(.+\): ([0-9]{1,10})\z");
        if (stop.Success && uint.TryParse(stop.Groups[1].Value, CultureInfo.InvariantCulture, out var status))
            return new("trace_stop_failed", "error", $"error: failed to terminate existing session: Windows error {status.ToString(CultureInfo.InvariantCulture)}");
        if (Match(text, "\\Awarning: a trace session named \\\".+\\\" is already running and it will be stopped\\.\\z").Success)
            return new("trace_session_replaced", "warning", "warning: a trace session is already running and it will be stopped: [session omitted]");
        if (Match(text, "\\Aerror: a trace session named \\\".+\\\" is already running\\. Use --stop_existing_session\\z").Success)
            return new("trace_session_conflict", "error", "error: a trace session is already running: [session omitted]");
        if (Match(text, @"\Aerror: failed to load manifest embedded in .+\z").Success)
            return new("embedded_manifest_failed", "error", "error: failed to load embedded manifest: [path omitted]");
        return new("unrecognized_output", severity, null);
    }

    private static Match Match(string text, string pattern) => Regex.Match(text, pattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(50));
}

internal sealed record PresentMonOutputSummary(long Characters, long Lines, long UnknownLines,
    bool ReadCompleted, bool ReadFailed, bool LimitReached, bool WarningReported,
    bool AccessDeniedReported, bool SessionConflictReported, bool LostEventsReported, bool LostBuffersReported)
{
    internal static PresentMonOutputSummary FromText(string text) => new(text.Length,
        text.Count(c => c == '\n'), 0, true, false, false,
        text.Contains("warning", StringComparison.OrdinalIgnoreCase),
        text.Contains("access denied", StringComparison.OrdinalIgnoreCase),
        text.Contains("already running", StringComparison.OrdinalIgnoreCase),
        text.Contains("lost event", StringComparison.OrdinalIgnoreCase) || text.Contains("events lost", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ETW events were lost", StringComparison.OrdinalIgnoreCase),
        text.Contains("buffers lost", StringComparison.OrdinalIgnoreCase) || text.Contains("ETW buffers were lost", StringComparison.OrdinalIgnoreCase));
}

// Drains both pipes independently of capture cancellation. Memory and persisted
// output are bounded even for very long lines, while the pipe continues to drain.
internal sealed class PresentMonDiagnosticOutput : IDisposable
{
    internal const int MaxRetainedCharacters = 65536;
    internal const int MaxLineCharacters = 4096;
    internal const int MaxLoggedLines = 128;
    private readonly CaptureDiagnosticLog _log;
    private readonly CaptureProcessRole _role;
    private readonly CaptureOutputStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private long _characters, _lines, _unknownLines;
    private bool _complete, _failed, _limited, _warning, _accessDenied, _sessionConflict, _eventsLost, _buffersLost;

    internal PresentMonDiagnosticOutput(TextReader reader, CaptureDiagnosticLog log, CaptureProcessRole role, CaptureOutputStream stream)
    {
        _log = log; _role = role; _stream = stream;
        Completion = ReadAsync(reader);
    }

    internal Task<string> Completion { get; }
    internal PresentMonOutputSummary Snapshot()
    {
        lock (_gate) return new(_characters, _lines, _unknownLines, _complete, _failed, _limited,
            _warning, _accessDenied, _sessionConflict, _eventsLost, _buffersLost);
    }

    private async Task<string> ReadAsync(TextReader reader)
    {
        var retained = new StringBuilder();
        var line = new StringBuilder();
        long lineCharacters = 0;
        var buffer = new char[1024];
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), _stop.Token).ConfigureAwait(false);
                if (read == 0) { lock (_gate) _complete = true; break; }
                lock (_gate)
                {
                    _characters += read;
                    if (_characters > MaxRetainedCharacters) _limited = true;
                }
                retained.Append(buffer, 0, Math.Min(read, MaxRetainedCharacters - retained.Length));
                for (var index = 0; index < read; index++)
                {
                    if (buffer[index] == '\n')
                    {
                        RecordLine(line, lineCharacters);
                        line.Clear(); lineCharacters = 0;
                    }
                    else
                    {
                        lineCharacters++;
                        if (line.Length < MaxLineCharacters) line.Append(buffer[index]);
                    }
                }
            }
        }
        catch (Exception) { lock (_gate) _failed = true; }
        finally { if (lineCharacters > 0) RecordLine(line, lineCharacters); }
        return retained.ToString();
    }

    private void RecordLine(StringBuilder line, long characters)
    {
        // Never accept a known prefix of an oversized line as a complete message.
        var truncated = characters > MaxLineCharacters;
        var message = truncated ? new PresentMonOutputMessage("unrecognized_output", "unknown", null)
            : PresentMonOutputMessages.Classify(line.ToString());
        lock (_gate)
        {
            _lines++;
            if (message.Code == "unrecognized_output") _unknownLines++;
            _warning |= message.Severity == "warning";
            _accessDenied |= message.Code == "trace_access_denied";
            _sessionConflict |= message.Code is "trace_session_replaced" or "trace_session_conflict";
            _eventsLost |= message.Code == "etw_events_lost";
            _buffersLost |= message.Code == "etw_buffers_lost";
            _limited |= truncated || _lines > MaxLoggedLines;
            if (_lines <= MaxLoggedLines)
                _log.ProcessOutput(_role, _stream, _lines, characters, truncated, message);
            else if (_lines == MaxLoggedLines + 1)
                _log.OutputLimit(_role, _stream, MaxLoggedLines);
        }
    }

    internal static async Task DrainAsync(PresentMonDiagnosticOutput stdout, PresentMonDiagnosticOutput stderr)
    {
        try { await Task.WhenAll(stdout.Completion, stderr.Completion).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
        catch (Exception)
        {
            stdout._stop.Cancel(); stderr._stop.Cancel();
            try { await Task.WhenAll(stdout.Completion, stderr.Completion).WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false); }
            catch (Exception) { /* Incomplete output is recorded in the stream summaries. */ }
        }
    }

    public void Dispose() { _stop.Cancel(); _stop.Dispose(); }
}
