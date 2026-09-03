using TarkovSkills.Core;

namespace TarkovSkills.Core.Tests;

public sealed class GoalStoreTests
{
    [Fact]
    public void MissingGoalUsesStableFpsDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var goal = new GoalStore(path).Load();
        Assert.Equal("stable-fps", goal.Goal);
        Assert.Equal(60, goal.TargetFpsMin);
        Assert.Equal("default", goal.Source);
    }

    [Fact]
    public void SavedGoalRoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TarkovSkillsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "goal.json");
        try
        {
            var store = new GoalStore(path);
            store.Save("better-graphics", 50, "quality first", "keep visibility");
            var loaded = store.Load();
            Assert.Equal("better-graphics", loaded.Goal);
            Assert.Equal(50, loaded.TargetFpsMin);
            Assert.Equal("quality first", loaded.QualityPreference);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(19)]
    [InlineData(361)]
    public void RejectsUnreasonableTarget(int target)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoalStore(path).Save("stable", target, "balanced"));
    }
}
