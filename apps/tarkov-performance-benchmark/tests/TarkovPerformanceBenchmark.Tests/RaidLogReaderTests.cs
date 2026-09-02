using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class RaidLogReaderTests
{
    [Fact]
    public void PostRaidProfileMarkerAfterGameStartEndsRaid()
    {
        var started = new DateTime(2026, 9, 2, 19, 23, 35, DateTimeKind.Local);
        var lines = new[]
        {
            "2026-09-02 19:22:32.700|Info|application|PrepareSelectedProfileLocally ProfileId:before AccountId:1",
            "2026-09-02 19:23:35.809|Info|application|GameStarted:28.6",
            "2026-09-02 19:24:58.919|Info|application|PrepareSelectedProfileLocally ProfileId:after AccountId:1"
        };

        Assert.True(RaidLogReader.ContainsRaidEndMarkerAfter(lines, started));
    }

    [Fact]
    public void ProfileMarkerBeforeGameStartDoesNotEndRaid()
    {
        var started = new DateTime(2026, 9, 2, 19, 23, 35, DateTimeKind.Local);
        var lines = new[]
        {
            "2026-09-02 19:22:32.700|Info|application|PrepareSelectedProfileLocally ProfileId:before AccountId:1",
            "2026-09-02 19:23:35.809|Info|application|GameStarted:28.6"
        };

        Assert.False(RaidLogReader.ContainsRaidEndMarkerAfter(lines, started));
    }
}
