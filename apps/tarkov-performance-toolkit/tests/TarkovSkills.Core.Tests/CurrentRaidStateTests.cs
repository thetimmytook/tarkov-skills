using TarkovSkills.Core;

namespace TarkovSkills.Core.Tests;

public sealed class CurrentRaidStateTests
{
    private static readonly DateTime StartedAt = new(2026, 10, 8, 10, 0, 5, DateTimeKind.Local);
    private const string MapLine = "2026-10-08 10:00:00.000|Info|application|scene preset path:maps/factory_day_preset.bundle";
    private const string StartLine = "2026-10-08 10:00:05.000|Info|application|GameStarted:28.6";
    private const string EndLine = "2026-10-08 10:02:05.000|Info|application|Got notification | UserMatchOver";

    [Fact]
    public void ClosedGameWithUnfinishedLogPreservesHistoryButHasNoActiveRaid()
    {
        using var logs = new LogFixture(MapLine, StartLine);
        var originalBytes = File.ReadAllBytes(logs.ApplicationLog);
        var historical = logs.Reader.Read(null);

        var current = logs.Reader.ReadCurrent(false, null);

        Assert.True(historical.Active);
        Assert.Equal("Factory", historical.Map);
        Assert.Equal("1.2.0.47888", historical.GameVersion);
        Assert.Equal(StartedAt, historical.StartedAt);
        Assert.Equal(historical with { Active = false }, current);
        Assert.Null(current.EndedAt);
        Assert.Equal(historical, logs.Reader.Read(null));
        Assert.Equal(originalBytes, File.ReadAllBytes(logs.ApplicationLog));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunningGameRetainsActiveRaidWithKnownOrUnavailableProcessStart(bool startTimeAvailable)
    {
        using var logs = new LogFixture(MapLine, StartLine);
        DateTime? processStart = startTimeAvailable ? StartedAt.AddMinutes(-2) : null;

        var current = logs.Reader.ReadCurrent(true, processStart);

        Assert.True(current.Active);
        Assert.Equal(logs.Reader.Read(processStart), current);
    }

    [Fact]
    public void RestartedGameDoesNotReactivateRaidFromPreviousProcess()
    {
        using var logs = new LogFixture(MapLine, StartLine);

        var current = logs.Reader.ReadCurrent(true, StartedAt.AddMinutes(5));

        Assert.False(current.Active);
        Assert.True(current.Found);
        Assert.Equal("Factory", current.Map);
        Assert.Equal(StartedAt, current.StartedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedRaidRetainsRealEndTimeRegardlessOfProcessPresence(bool running)
    {
        using var logs = new LogFixture(MapLine, StartLine, EndLine);

        var current = logs.Reader.ReadCurrent(running, null);

        Assert.False(current.Active);
        Assert.Equal(StartedAt.AddMinutes(2), current.EndedAt);
        Assert.Equal(logs.Reader.Read(null), current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapLogWithoutRaidStartIsNeverActive(bool running)
    {
        using var logs = new LogFixture(MapLine);

        var current = logs.Reader.ReadCurrent(running, null);

        Assert.False(current.Active);
        Assert.True(current.Found);
        Assert.Equal("Factory", current.Map);
        Assert.Null(current.StartedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingLogsRemainUnknownAndInactive(bool running)
    {
        var reader = new RaidLogReader(() => null);

        var current = reader.ReadCurrent(running, null);

        Assert.False(current.Active);
        Assert.False(current.Found);
        Assert.Equal("unknown", current.Map);
        Assert.Null(current.StartedAt);
        Assert.Null(current.EndedAt);
    }

    private sealed class LogFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "raid-state-test-" + Guid.NewGuid().ToString("N"));
        public string ApplicationLog { get; }
        public RaidLogReader Reader { get; }

        public LogFixture(params string[] lines)
        {
            var folder = Directory.CreateDirectory(Path.Combine(_directory, "log_2026.10.08_1.2.0.47888"));
            ApplicationLog = Path.Combine(folder.FullName, "application.log");
            File.WriteAllLines(ApplicationLog, lines);
            Reader = new RaidLogReader(() => _directory);
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
