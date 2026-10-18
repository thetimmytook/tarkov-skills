using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace TarkovSkills.Core.Tests;

public sealed class CaptureDiagnosticTests : IDisposable
{
    [Fact]
    public void PresentMonNativeLostEventWarningIsRecognized()
    {
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        log.PresentMonExited(0, 120, false, "", "warning: 27 ETW events were lost.\r\nwarning: 3 ETW buffers were lost.\r\n");
        var data = Event("presentmon_exited").GetProperty("data");
        Assert.True(data.GetProperty("lost_events_reported").GetBoolean());
        Assert.True(data.GetProperty("lost_buffers_reported").GetBoolean());
    }

    [Fact]
    public void RejectedFramesHaveSeparateReasonsAndLongFrameDuration()
    {
        var path = Csv("TimeInMs,MsBetweenPresents", new[] { "0,10", "10,NA", "20,0", "30,10000", "40,NaN", "50,-1", "60" });
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        Assert.Throws<InvalidDataException>(() => PresentMonCsvParser.ParseCapture(path, log));
        var parsed = Event("csv_summary").GetProperty("data");
        var rejected = parsed.GetProperty("rejected_frame_reasons");
        Assert.Equal(1, rejected.GetProperty("missing_value").GetInt32());
        Assert.Equal(2, rejected.GetProperty("non_numeric_or_non_finite").GetInt32());
        Assert.Equal(2, rejected.GetProperty("non_positive").GetInt32());
        Assert.Equal(1, rejected.GetProperty("at_least_10_seconds").GetInt32());
        Assert.Equal(10, parsed.GetProperty("rejected_long_frametime_sum_seconds").GetDouble());
        Assert.Equal(10000, parsed.GetProperty("largest_finite_frametime_ms").GetDouble());
    }

    [Fact]
    public void LongRejectedFrameLeavesAnExplicitGapAndUnchangedFpsDuration()
    {
        string Row(double seconds, double frame) =>
            $"{(ulong)Math.Round(seconds * Stopwatch.Frequency)},{frame.ToString(CultureInfo.InvariantCulture)}";
        var rows = Enumerable.Range(1, 60).Select(i => Row(1 + i * .01, 10))
            .Append(Row(107.6, 106000))
            .Concat(Enumerable.Range(1, 60).Select(i => Row(107.6 + i * .01, 10)));
        var path = Csv("TimeInQPC,MsBetweenPresents", rows);
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var capture = PresentMonCsvParser.ParseCapture(path, log);
        Assert.Equal(120, capture.Metrics.SampleCount);
        Assert.Equal(1.2, capture.Metrics.DurationSec);
        Assert.Equal(2, capture.Window.Intervals.Count);
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(106, parsed.GetProperty("rejected_long_frametime_sum_seconds").GetDouble(), 6);
        Assert.Equal(1, parsed.GetProperty("valid_frame_window_gap_count").GetInt32());
        Assert.Equal(106, parsed.GetProperty("valid_frame_window_largest_gap_seconds").GetDouble(), 6);
        Assert.Equal(106, parsed.GetProperty("valid_frame_window_gap_seconds").GetDouble(), 6);
        Assert.Equal(1.2, parsed.GetProperty("valid_frame_window_seconds").GetDouble(), 6);
        Assert.True(parsed.GetProperty("qpc_alignment_available").GetBoolean());
    }

    [Fact]
    public void ContinuousFramesReportZeroGapsAndUnknownTimestampsReportNull()
    {
        var evidence = new CaptureParseDiagnostics("missing-fixture.csv");
        evidence.Window(CaptureWindow.FromFrames([new(1, 2), new(2, 3)]), true);
        var known = JsonSerializer.SerializeToElement(evidence.Snapshot());
        Assert.Equal(2, known.GetProperty("valid_frame_window_seconds").GetDouble());
        Assert.Equal(0, known.GetProperty("valid_frame_window_gap_count").GetInt32());
        Assert.Equal(0, known.GetProperty("valid_frame_window_largest_gap_seconds").GetDouble());

        evidence = new CaptureParseDiagnostics("missing-fixture.csv");
        evidence.Window(CaptureWindow.Unknown, false);
        var unknown = JsonSerializer.SerializeToElement(evidence.Snapshot());
        Assert.False(unknown.GetProperty("qpc_alignment_available").GetBoolean());
        foreach (var field in new[] { "valid_frame_window_seconds", "valid_frame_window_gap_count",
            "valid_frame_window_gap_seconds", "valid_frame_window_largest_gap_seconds" })
            Assert.Equal(JsonValueKind.Null, unknown.GetProperty(field).ValueKind);
    }

    [Fact]
    public async Task ExtremeRejectedValuesCannotSuppressTheFinalDiagnosticRecord()
    {
        var path = Csv("MsBetweenPresents", new[] { "1e308", "1e308", "Infinity", "NaN" });
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        await Assert.ThrowsAsync<InvalidDataException>(() => CaptureDiagnostics.ObserveAsync(log,
            () => Task.FromResult(PresentMonCsvParser.ParseCapture(path, log))));
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(JsonValueKind.Null, parsed.GetProperty("rejected_long_frametime_sum_seconds").ValueKind);
        var rejected = parsed.GetProperty("rejected_frame_reasons");
        Assert.Equal(2, rejected.GetProperty("at_least_10_seconds").GetInt32());
        Assert.Equal(2, rejected.GetProperty("non_numeric_or_non_finite").GetInt32());
        Assert.Equal("failed", Event("finished").GetProperty("data").GetProperty("outcome").GetString());
    }

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "capture-log-test-" + Guid.NewGuid().ToString("N"));
    private string Csv(string header, IEnumerable<string> rows)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "frames.csv");
        File.WriteAllLines(path, new[] { header }.Concat(rows));
        return path;
    }
    private JsonElement Event(string stage)
    {
        var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Select(line => JsonDocument.Parse(line).RootElement.Clone()).Last(e => e.GetProperty("stage").GetString() == stage);
    }
    private string ReadLog()
    {
        using var reader = new StreamReader(new FileStream(Assert.Single(Directory.GetFiles(_directory, "*.log")),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ShortCaptureKeepsParserEvidenceAndDiscardReasonWithoutChangingTheException()
    {
        var path = Csv("TimeInMs,MsBetweenPresents", Enumerable.Range(1, 120).Select(i => $"{i * 10},10"));
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var raid = new RaidContext(true, true, "Woods", "woods", null, DateTime.Now, null);
        await Assert.ThrowsAsync<CaptureDiscardedException>(() => CaptureDiagnostics.ObserveAsync(log, () =>
        {
            var capture = PresentMonCsvParser.ParseCapture(path, log);
            CaptureValidation.EnsureComplete(capture.Metrics, raid, false, 120);
            return Task.FromResult(capture);
        }));
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(120, parsed.GetProperty("valid_frames").GetInt32());
        Assert.Equal(1.2, parsed.GetProperty("frametime_sum_seconds").GetDouble(), 6);
        Assert.Equal(1.19, parsed.GetProperty("relative_timestamp_span_seconds").GetDouble(), 6);
        Assert.False(parsed.GetProperty("qpc_alignment_available").GetBoolean());
        var ended = Event("finished").GetProperty("data");
        Assert.Equal("discarded", ended.GetProperty("outcome").GetString());
        Assert.Equal("insufficient_valid_duration", ended.GetProperty("reason").GetString());
        Assert.Equal(120, Event("started").GetProperty("data").GetProperty("requested_seconds").GetInt32());
    }

    [Fact]
    public async Task ParserFailureStillFlushesCountsAndMissingColumns()
    {
        var path = Csv("MsBetweenPresents", new[] { "10", "NA", "0", "10000", "NaN", "-1" });
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        await Assert.ThrowsAsync<InvalidDataException>(() => CaptureDiagnostics.ObserveAsync(log, () =>
            Task.FromResult(PresentMonCsvParser.ParseCapture(path, log))));
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(6, parsed.GetProperty("rows_read").GetInt32());
        Assert.Equal(1, parsed.GetProperty("valid_frames").GetInt32());
        Assert.Equal(5, parsed.GetProperty("rejected_frames").GetInt32());
        Assert.Equal("too_few_frames", Event("finished").GetProperty("data").GetProperty("reason").GetString());
    }

    [Fact]
    public void TruncatedRowIsReportedWithoutChangingExistingParserResults()
    {
        var path = Csv("Application,TimeInMs,MsBetweenPresents,Extra", Enumerable.Range(1, 120).Select(i => $"EscapeFromTarkov.exe,{i * 100},100,x"));
        File.AppendAllText(path, "EscapeFromTarkov.exe,12100,100");
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var capture = PresentMonCsvParser.ParseCapture(path, log);
        Assert.Equal(121, capture.Metrics.SampleCount);
        Assert.Equal(12.1, capture.Metrics.DurationSec);
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(1, parsed.GetProperty("column_count_mismatches").GetInt32());
        Assert.False(parsed.GetProperty("ends_with_newline").GetBoolean());
    }

    [Fact]
    public async Task PrivateCsvFieldsAndExceptionMessagesNeverEnterTheLog()
    {
        const string privateText = @"C:\Users\private-person\secret-token.txt";
        var path = Csv("Application,ProcessID,SwapChainAddress,MsBetweenPresents," + privateText,
            Enumerable.Repeat("OtherPrivateApp.exe,987654,0xABC123,100," + privateText, 120));
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var error = new InvalidOperationException(privateText);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => CaptureDiagnostics.ObserveAsync<int>(log, () =>
        {
            PresentMonCsvParser.ParseCapture(path, log);
            throw error;
        }));
        Assert.Same(error, thrown);
        log.PresentMonExited(2, 1.5, false, privateText, "access denied " + privateText);
        var text = ReadLog();
        foreach (var secret in new[] { "private-person", "secret-token", "OtherPrivateApp", "987654", "ABC123", "ProcessID", "SwapChainAddress" })
            Assert.DoesNotContain(secret, text);
        Assert.Equal(120, Event("csv_summary").GetProperty("data").GetProperty("other_application_rows").GetInt32());
        Assert.True(Event("presentmon_exited").GetProperty("data").GetProperty("access_denied_reported").GetBoolean());
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("completed")]
    public async Task CancellationAndSuccessHaveTerminalRecords(string outcome)
    {
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        if (outcome == "cancelled")
            await Assert.ThrowsAsync<OperationCanceledException>(() => CaptureDiagnostics.ObserveAsync<int>(log, () => throw new OperationCanceledException()));
        else
            Assert.Equal(42, await CaptureDiagnostics.ObserveAsync(log, () => Task.FromResult(42)));
        Assert.Equal(outcome, Event("finished").GetProperty("data").GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task MalformedCsvPreservesPriorCountsWithoutCopyingTheBadLine()
    {
        var path = Csv("MsBetweenPresents,Extra", new[] { "100,ok", "\"private unfinished value" });
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        await Assert.ThrowsAsync<Microsoft.VisualBasic.FileIO.MalformedLineException>(() =>
            CaptureDiagnostics.ObserveAsync(log, () => Task.FromResult(PresentMonCsvParser.ParseCapture(path, log))));
        var parsed = Event("csv_summary").GetProperty("data");
        Assert.Equal(1, parsed.GetProperty("valid_frames").GetInt32());
        Assert.Equal(1, parsed.GetProperty("malformed_csv_rows").GetInt32());
        Assert.DoesNotContain("private unfinished value", ReadLog());
    }

    [Fact]
    public async Task UnwritableLogLocationDoesNotBreakTheCapture()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "file-instead-of-folder");
        File.WriteAllText(file, "unchanged");
        using var log = CaptureDiagnosticLog.Create(file, 120);
        Assert.Equal(42, await CaptureDiagnostics.ObserveAsync(log, () => Task.FromResult(42)));
        Assert.Equal("unchanged", File.ReadAllText(file));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
