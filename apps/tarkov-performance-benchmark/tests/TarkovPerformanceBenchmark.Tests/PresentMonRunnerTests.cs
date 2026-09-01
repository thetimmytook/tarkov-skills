using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class PresentMonRunnerTests
{
    [Fact]
    public void CaptureUsesAppOwnedReplaceableSession()
    {
        var arguments = PresentMonRunner.BuildArguments(120, "capture.csv");

        Assert.Contains("--session_name", arguments);
        Assert.Contains(PresentMonRunner.OwnedSessionName, arguments);
        Assert.Contains("--stop_existing_session", arguments);
        Assert.DoesNotContain("1.0", PresentMonRunner.OwnedSessionName);
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
