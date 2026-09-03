using TarkovBenchmark.Feature;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class ShellContractTests
{
    [Fact]
    public void StandaloneBenchmarkKeepsWebCopyActionHidden()
    {
        var options = BenchmarkFeatureOptions.ForStandalone("1.0.0", false, false, null);

        Assert.False(options.ShowCopyResults);
        Assert.False(options.CollectRequested);
        Assert.False(options.SourceSkill);
    }
}
