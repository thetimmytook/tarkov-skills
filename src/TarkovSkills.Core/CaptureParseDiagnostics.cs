using System.Diagnostics;
using System.Globalization;

namespace TarkovSkills.Core;

internal sealed class CaptureParseDiagnostics
{
    private static readonly string[] KnownColumns = ["MsBetweenPresents", "FrameTime", "CPUFrameTime",
        "MsBetweenDisplayChange", "TimeInQPC", "QPCTime", "CPUStartQPC", "TimeInMs", "CPUStartTimeInMs"];
    private string[] _headers = [];
    private string _frameColumn = "unavailable";
    private int _application = -1, _relativeTime = -1, _qpc = -1, _frame = -1;
    private int _rows, _valid, _mismatches, _tarkov, _unknown, _other, _qpcValid, _malformed;
    private int _missing, _nonNumeric, _nonPositive, _longFrames;
    private double _sum;
    private double _longSum;
    private double? _largestFrame, _windowSeconds, _gapSeconds, _largestGap;
    private int? _gapCount;
    private double? _relativeMin, _relativeMax, _qpcMin, _qpcMax;
    private readonly long? _bytes;
    private readonly bool? _newline;
    internal bool AlignmentAvailable { get; private set; }
    internal void MalformedRow() => _malformed++;

    internal CaptureParseDiagnostics(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            _bytes = file.Length;
            if (file.Length > 0) { file.Seek(-1, SeekOrigin.End); _newline = file.ReadByte() == '\n'; }
        }
        catch { }
    }

    internal void Header(string[] headers, int frame)
    {
        _headers = headers;
        _frame = frame;
        _frameColumn = frame < 0 ? "unavailable" : KnownColumns.Contains(headers[frame], StringComparer.OrdinalIgnoreCase)
            ? KnownColumns.First(c => c.Equals(headers[frame], StringComparison.OrdinalIgnoreCase)) : "other_frametime";
        _application = Find("Application");
        _relativeTime = Find("TimeInMs");
        _qpc = _frameColumn == "MsBetweenPresents" ? Find("TimeInQPC", "QPCTime") : Find("CPUStartQPC");
    }

    internal void Row(string[]? row, double? frame)
    {
        _rows++;
        if (row is null || row.Length != _headers.Length) _mismatches++;
        if (_frame < 0 || row is null || row.Length <= _frame) _missing++;
        else if (!Number(row[_frame], out var rawFrame)) _nonNumeric++;
        else
        {
            _largestFrame = _largestFrame.HasValue ? Math.Max(_largestFrame.Value, rawFrame) : rawFrame;
            if (rawFrame <= 0) _nonPositive++;
            else if (rawFrame >= 10000) { _longFrames++; _longSum += rawFrame; }
        }
        if (row is null) return;
        if (_application >= 0 && row.Length > _application)
        {
            var app = row[_application];
            if (app.Equals("EscapeFromTarkov.exe", StringComparison.OrdinalIgnoreCase)) _tarkov++;
            else if (app == "<unknown>" || string.IsNullOrWhiteSpace(app)) _unknown++;
            else _other++;
        }
        if (frame is null) return;
        _valid++; _sum += frame.Value;
        if (_relativeTime >= 0 && row.Length > _relativeTime && Number(row[_relativeTime], out var time))
            Range(ref _relativeMin, ref _relativeMax, time / 1000);
        if (_qpc >= 0 && row.Length > _qpc && ulong.TryParse(row[_qpc], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks > 0)
        {
            _qpcValid++;
            Range(ref _qpcMin, ref _qpcMax, ticks / (double)Stopwatch.Frequency);
        }
    }

    internal void Window(CaptureWindow window, bool aligned)
    {
        AlignmentAvailable = aligned && window.Intervals.Count > 0;
        if (!aligned || window.Intervals.Count == 0) return;
        _windowSeconds = window.Duration;
        _gapCount = window.Intervals.Count - 1;
        _gapSeconds = 0;
        _largestGap = 0;
        for (var i = 1; i < window.Intervals.Count; i++)
        {
            var gap = window.Intervals[i].Start - window.Intervals[i - 1].End;
            _gapSeconds += gap;
            _largestGap = Math.Max(_largestGap.Value, gap);
        }
    }

    internal object Snapshot() => new {
        csv_bytes = _bytes, ends_with_newline = _newline,
        known_columns = KnownColumns.Where(c => _headers.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray(),
        selected_frametime_column = _frameColumn, header_column_count = _headers.Length,
        rows_read = _rows, valid_frames = _valid, rejected_frames = _rows - _valid,
        rejected_frame_reasons = new { missing_value = _missing, non_numeric_or_non_finite = _nonNumeric,
            non_positive = _nonPositive, at_least_10_seconds = _longFrames },
        largest_finite_frametime_ms = Rounded(_largestFrame),
        rejected_long_frametime_sum_seconds = double.IsFinite(_longSum) ? (double?)Math.Round(_longSum / 1000, 6) : null,
        valid_frame_window_seconds = Rounded(_windowSeconds), valid_frame_window_gap_count = _gapCount,
        valid_frame_window_gap_seconds = Rounded(_gapSeconds), valid_frame_window_largest_gap_seconds = Rounded(_largestGap),
        malformed_csv_rows = _malformed,
        column_count_mismatches = _mismatches, frametime_sum_seconds = Math.Round(_sum / 1000, 6),
        relative_timestamp_span_seconds = Span(_relativeMin, _relativeMax),
        qpc_timestamp_span_seconds = Span(_qpcMin, _qpcMax), valid_qpc_rows = _qpcValid,
        qpc_alignment_available = AlignmentAvailable, application_column_present = _application >= 0,
        tarkov_application_rows = _tarkov, unknown_application_rows = _unknown, other_application_rows = _other
    };

    private int Find(params string[] names) => Array.FindIndex(_headers, h => names.Contains(h, StringComparer.OrdinalIgnoreCase));
    private static bool Number(string value, out double number) => double.TryParse(value.Replace(',', '.'),
        NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    private static void Range(ref double? min, ref double? max, double value)
    { min = min.HasValue ? Math.Min(min.Value, value) : value; max = max.HasValue ? Math.Max(max.Value, value) : value; }
    private static double? Span(double? min, double? max) => min.HasValue && max.HasValue ? Math.Round(max.Value - min.Value, 6) : null;
    private static double? Rounded(double? value) => value.HasValue ? Math.Round(value.Value, 6) : null;
}
