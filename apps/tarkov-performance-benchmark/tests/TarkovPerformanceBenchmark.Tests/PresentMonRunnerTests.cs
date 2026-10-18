using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class PresentMonRunnerTests
{
    [Theory]
    [InlineData(PresentMonRunner.OwnedSessionName)]
    [InlineData("PresentMon")]
    public void CleanupTerminatesTheNamedSessionWithoutStartingAnotherCapture(string sessionName)
    {
        var arguments = PresentMonRunner.BuildStopSessionArguments(sessionName);

        Assert.Contains("--terminate_existing_session", arguments);
        Assert.DoesNotContain("--stop_existing_session", arguments);
        var nameIndex = arguments.ToList().IndexOf("--session_name");
        Assert.Equal(sessionName, arguments[nameIndex + 1]);
    }

    [Fact]
    public void CaptureUsesAppOwnedReplaceableSession()
    {
        var arguments = PresentMonRunner.BuildArguments(120, "capture.csv");

        Assert.Contains("--session_name", arguments);
        Assert.Contains(PresentMonRunner.OwnedSessionName, arguments);
        Assert.Contains("--stop_existing_session", arguments);
        Assert.DoesNotContain("1.0", PresentMonRunner.OwnedSessionName);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(240)]
    public void FpsCaptureDoesNotWaitForGpuOrDisplayTracking(int duration)
    {
        var arguments = PresentMonRunner.BuildArguments(duration, "capture.csv");

        Assert.Contains("--no_track_gpu", arguments);
        Assert.Contains("--no_track_display", arguments);
        Assert.Contains("--qpc_time", arguments);
        Assert.DoesNotContain("--exclude_dropped", arguments);
        var durationIndex = arguments.ToList().IndexOf("--timed");
        Assert.Equal(duration.ToString(), arguments[durationIndex + 1]);
        Assert.Contains("--terminate_after_timed", arguments);
        Assert.Contains("--terminate_on_proc_exit", arguments);
    }

    [Fact]
    public void GenericPresentMonFailureDoesNotExposeRawOutput()
    {
        var exception = PresentMonRunner.CreateExitException(13, @"failure at C:\Users\private\capture.csv");

        Assert.IsType<InvalidOperationException>(exception);
        Assert.DoesNotContain(@"C:\Users\", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exit code 13", exception.Message);
    }

    [Fact]
    public void ExistingSessionGetsFriendlyException()
    {
        var exception = PresentMonRunner.CreateExitException(1, "warning: elevated privilege may be required\nerror: PresentMon is already running");

        Assert.IsType<PresentMonSessionException>(exception);
        Assert.DoesNotContain("PresentMon is already running", exception.Message);
    }

    [Fact]
    public void CleanupFallbackStopsOnlyTheNamedEtwSession()
    {
        var arguments = PresentMonRunner.BuildLogmanStopArguments(PresentMonRunner.OwnedSessionName);

        Assert.Equal(["stop", PresentMonRunner.OwnedSessionName, "-ets"], arguments);
    }
}
