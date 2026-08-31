using System.Globalization;
using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class PresentMonCsvParserTests
{
    [Fact]
    public void ParsesPresentMonV1FrametimeMetrics()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var rows = Enumerable.Range(0, 1000).Select(index => $"EscapeFromTarkov.exe,{(index < 10 ? 25d : 16.6667d).ToString(CultureInfo.InvariantCulture)}");
            File.WriteAllLines(path, new[] { "Application,MsBetweenPresents" }.Concat(rows));
            var result = PresentMonCsvParser.Parse(path);
            Assert.Equal(1000, result.SampleCount);
            Assert.InRange(result.AverageFps, 59, 60);
            Assert.Equal(40, result.OnePercentLowFps);
            Assert.Equal(16.667, result.P95FrametimeMs);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParsesSkillInvocation()
    {
        var invocation = AppInvocation.Parse(["collect", "--source", "skill"]);
        Assert.True(invocation.CollectRequested);
        Assert.True(invocation.SourceSkill);
    }
}
