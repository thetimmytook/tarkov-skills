using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class CaptureValidationTests
{
    [Fact]
    public void RejectsCaptureWhenTarkovExited()
    {
        var exception = Assert.Throws<CaptureDiscardedException>(() => CaptureValidation.EnsureComplete(Metrics(120), ActiveContext(), true));
        Assert.Contains("Tarkov closed", exception.Message);
    }

    [Fact]
    public void RejectsCaptureWhenRaidEnded()
    {
        var context = ActiveContext() with { Active = false, EndedAt = DateTime.Now };
        var exception = Assert.Throws<CaptureDiscardedException>(() => CaptureValidation.EnsureComplete(Metrics(120), context, false));
        Assert.Contains("raid ended", exception.Message);
    }

    [Fact]
    public void RejectsCaptureThatIsTooShort()
    {
        var exception = Assert.Throws<CaptureDiscardedException>(() => CaptureValidation.EnsureComplete(Metrics(60), ActiveContext(), false));
        Assert.Contains("partial result was discarded", exception.Message);
    }

    [Fact]
    public void AcceptsCompleteActiveRaidCapture() => CaptureValidation.EnsureComplete(Metrics(120), ActiveContext(), false);

    private static RaidContext ActiveContext() => new(true, true, "Factory", "factory4_day", "1.0", DateTime.Now.AddMinutes(-5), null);
    private static PerformanceMetrics Metrics(double duration) => new(1000, duration, 100, 70, 50, 10, 15, 20);
}
