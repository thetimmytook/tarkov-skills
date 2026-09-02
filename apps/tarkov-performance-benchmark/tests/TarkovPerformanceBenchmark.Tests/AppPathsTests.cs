using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class AppPathsTests
{
    private const string LocalAppData = @"C:\Users\player\AppData\Local";

    [Fact]
    public void UnpackagedBuildUsesSharedDevelopmentDirectory()
    {
        Assert.Equal(
            @"C:\Users\player\AppData\Local\TarkovSkills",
            AppPaths.ResolveDataDirectory(LocalAppData, null));
    }

    [Fact]
    public void StoreBuildUsesPackageLocalState()
    {
        Assert.Equal(
            @"C:\Users\player\AppData\Local\Packages\TimmyTook.TarkovPerformanceBenchmark_test\LocalState\TarkovSkills",
            AppPaths.ResolveDataDirectory(LocalAppData, "TimmyTook.TarkovPerformanceBenchmark_test"));
    }
}
