using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace TarkovSkills.Core.Tests;

public sealed class PresentMonDiagnosticOutputTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "presentmon-output-test-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(CaptureProcessRole.capture, CaptureOutputStream.stderr)]
    [InlineData(CaptureProcessRole.final_cleanup, CaptureOutputStream.stdout)]
    internal async Task OutputIsFlushedBeforeProcessExitAndUnterminatedLastLineIsPreserved(CaptureProcessRole role, CaptureOutputStream stream)
    {
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var pipe = new Pipe();
        using var reader = new StreamReader(pipe.Reader.AsStream());
        using var output = new PresentMonDiagnosticOutput(reader, log, role, stream);
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("warning: 27 ETW events were lost.\r\n"));
        await WaitForLogAsync("etw_events_lost");
        Assert.False(output.Completion.IsCompleted);
        var first = Events("presentmon_output").Single().GetProperty("data");
        Assert.Equal(role.ToString(), first.GetProperty("role").GetString());
        Assert.Equal(stream.ToString(), first.GetProperty("stream").GetString());
        Assert.True(Events("presentmon_output").Single().GetProperty("elapsed_seconds").GetDouble() >= 0);
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("Stopped recording."));
        await pipe.Writer.CompleteAsync();
        var raw = await output.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("warning: 27 ETW events were lost.\r\nStopped recording.", raw);
        Assert.Equal("recording_stopped", Events("presentmon_output").Last().GetProperty("data").GetProperty("message_code").GetString());
        Assert.Equal(2, output.Snapshot().Lines);
        Assert.True(output.Snapshot().ReadCompleted);
        Assert.True(output.Snapshot().LostEventsReported);
    }

    [Fact]
    public async Task BothPipesDrainAndWarningsSurviveCancellationOutcome()
    {
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        using var stdout = new PresentMonDiagnosticOutput(new StringReader("Started recording.\n"), log,
            CaptureProcessRole.capture, CaptureOutputStream.stdout);
        using var stderr = new PresentMonDiagnosticOutput(new StringReader("warning: 3 ETW buffers were lost."), log,
            CaptureProcessRole.capture, CaptureOutputStream.stderr);
        await PresentMonDiagnosticOutput.DrainAsync(stdout, stderr);
        log.ProcessExited(CaptureProcessRole.capture, -1, 20, true, false, stdout.Snapshot(), stderr.Snapshot());
        await Assert.ThrowsAsync<OperationCanceledException>(() => CaptureDiagnostics.ObserveAsync<int>(log,
            () => throw new OperationCanceledException()));
        var exit = Events("presentmon_exited").Single().GetProperty("data");
        Assert.True(exit.GetProperty("cancellation_requested").GetBoolean());
        Assert.True(exit.GetProperty("lost_buffers_reported").GetBoolean());
        Assert.True(exit.GetProperty("stdout_characters").GetInt32() > 0);
        Assert.True(exit.GetProperty("stderr_characters").GetInt32() > 0);
        Assert.Equal("cancelled", Events("finished").Single().GetProperty("data").GetProperty("outcome").GetString());
        Assert.Equal(2, Events("presentmon_output").Count());
    }

    [Fact]
    public async Task StalledPipesHaveBoundedDrainAndExplicitIncompleteStatus()
    {
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        var pipe = new Pipe();
        using var reader = new StreamReader(pipe.Reader.AsStream());
        using var stalled = new PresentMonDiagnosticOutput(reader, log, CaptureProcessRole.capture, CaptureOutputStream.stderr);
        using var finished = new PresentMonDiagnosticOutput(new StringReader(""), log, CaptureProcessRole.capture, CaptureOutputStream.stdout);
        await PresentMonDiagnosticOutput.DrainAsync(finished, stalled).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stalled.Snapshot().ReadCompleted);
        Assert.True(stalled.Snapshot().ReadFailed);
        Assert.True(finished.Snapshot().ReadCompleted);
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task PinnedPrivilegeWarningIsPreservedAsFourSafeMessages()
    {
        const string warning = "warning: PresentMon requires elevated privilege in order to query processes that are\r\n"
            + "         short-running or started on another account.  Without it, those processes will\r\n"
            + "         be listed as '<unknown>' and they can't be targeted by --process_name nor trigger\r\n"
            + "         --terminate_on_proc_exit.\r\n";
        Assert.Equal(303, warning.Length);
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        using var output = new PresentMonDiagnosticOutput(new StringReader(warning), log, CaptureProcessRole.capture, CaptureOutputStream.stderr);
        await output.Completion;
        Assert.Equal(303, output.Snapshot().Characters);
        Assert.Equal(0, output.Snapshot().UnknownLines);
        Assert.True(output.Snapshot().WarningReported);
        Assert.False(output.Snapshot().AccessDeniedReported);
        Assert.Equal(4, Events("presentmon_output").Count());
        Assert.All(Events("presentmon_output"), item => Assert.Equal(JsonValueKind.String, item.GetProperty("data").GetProperty("message").ValueKind));
    }

    [Fact]
    public async Task VariableSessionsPathsAndUnknownOutputNeverEnterTheFile()
    {
        const string secrets = @"C:\Users\private-person\token-secret.txt OtherPrivateApp.exe pid=987654 192.0.2.42 device-secret";
        var lines = new[]
        {
            "error: no existing sessions found: " + secrets,
            "error: failed to terminate existing session (" + secrets + "): 5",
            "warning: a trace session named \"" + secrets + "\" is already running and it will be stopped.",
            "error: a trace session named \"" + secrets + "\" is already running. Use --stop_existing_session",
            "error: failed to load manifest embedded in " + secrets,
            "warning: unknown reason " + secrets,
            "Started recording. " + secrets,
            secrets,
            "warning: 27 ETW events were lost. " + secrets
        };
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        using var output = new PresentMonDiagnosticOutput(new StringReader(string.Join('\n', lines)), log,
            CaptureProcessRole.prepare_cleanup, CaptureOutputStream.stderr);
        await output.Completion;
        var text = ReadLog();
        foreach (var secret in new[] { "private-person", "token-secret", "OtherPrivateApp", "987654", "192.0.2.42", "device-secret", "C:\\Users" })
            Assert.DoesNotContain(secret, text);
        Assert.Equal(4, output.Snapshot().UnknownLines);
        Assert.True(output.Snapshot().SessionConflictReported);
        Assert.False(output.Snapshot().LostEventsReported);
        Assert.Contains("Windows error 5", text);
        Assert.Contains("[path omitted]", text);
        Assert.All(Events("presentmon_output").Where(e => e.GetProperty("data").GetProperty("message_code").GetString() == "unrecognized_output"),
            item => Assert.Equal(JsonValueKind.Null, item.GetProperty("data").GetProperty("message").ValueKind));
    }

    [Fact]
    public async Task HugeLinesAndRepeatedMessagesAreBoundedWhileTheWholePipeIsDrained()
    {
        var text = "Started recording." + new string('x', 200000) + "\n"
            + string.Concat(Enumerable.Repeat("warning: 1 ETW events were lost.\n", 1000));
        using var log = CaptureDiagnosticLog.Create(_directory, 120);
        using var output = new PresentMonDiagnosticOutput(new StringReader(text), log, CaptureProcessRole.capture, CaptureOutputStream.stderr);
        var retained = await output.Completion;
        Assert.Equal(PresentMonDiagnosticOutput.MaxRetainedCharacters, retained.Length);
        Assert.Equal(text.Length, output.Snapshot().Characters);
        Assert.Equal(1001, output.Snapshot().Lines);
        Assert.Equal(1, output.Snapshot().UnknownLines);
        Assert.True(output.Snapshot().LimitReached);
        Assert.True(output.Snapshot().ReadCompleted);
        Assert.Equal(PresentMonDiagnosticOutput.MaxLoggedLines, Events("presentmon_output").Count());
        Assert.Single(Events("presentmon_output_limit"));
        Assert.Equal("unrecognized_output", Events("presentmon_output").First().GetProperty("data").GetProperty("message_code").GetString());
        Assert.DoesNotContain(new string('x', 20), ReadLog());
        Assert.True(new FileInfo(Directory.GetFiles(_directory, "*.log").Single()).Length < 100000);
    }

    [Fact]
    public async Task UnwritableLogDoesNotStopPipeReadingOrChangeExitClassification()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(file, "unchanged");
        using var log = CaptureDiagnosticLog.Create(file, 120);
        using var output = new PresentMonDiagnosticOutput(new StringReader("error: failed to start trace session: access denied.\n"), log,
            CaptureProcessRole.capture, CaptureOutputStream.stderr);
        var raw = await output.Completion;
        Assert.IsType<PresentMonPermissionException>(PresentMonRunner.CreateExitException(6, raw));
        Assert.True(output.Snapshot().AccessDeniedReported);
        Assert.True(output.Snapshot().ReadCompleted);
        Assert.Equal("unchanged", File.ReadAllText(file));
    }

    private async Task WaitForLogAsync(string expected)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var text = ReadLog();
            if (text.EndsWith('\n') && text.Contains(expected, StringComparison.Ordinal)) return;
            await Task.Delay(10);
        }
        Assert.Contains(expected, ReadLog());
    }

    private IEnumerable<JsonElement> Events(string stage) => ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement.Clone()).Where(e => e.GetProperty("stage").GetString() == stage);

    private string ReadLog()
    {
        using var reader = new StreamReader(new FileStream(Directory.GetFiles(_directory, "*.log").Single(),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd();
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
