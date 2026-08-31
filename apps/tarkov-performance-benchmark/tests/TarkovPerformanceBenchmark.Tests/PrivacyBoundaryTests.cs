using System.Diagnostics;
using System.Text.Json;
using TarkovPerformanceBenchmark;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class PrivacyBoundaryTests
{
    [Fact]
    public void SettingsSnapshotContainsOnlyDiagnosticFilesAndNoPaths()
    {
        var snapshot = new SettingsReader().Read();
        var json = JsonSerializer.Serialize(snapshot.Settings, JsonDefaults.Options);
        Assert.Contains("graphics", json);
        Assert.Contains("postfx", json);
        Assert.Contains("game", json);
        Assert.DoesNotContain("Control.ini", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sound.ini", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings_dir", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemSnapshotDoesNotContainLocalIdentityOrPaths()
    {
        var snapshot = new SystemInfoCollector().Collect();
        var json = JsonSerializer.Serialize(snapshot.System, JsonDefaults.Options);
        Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\", json, StringComparison.OrdinalIgnoreCase);
    }
}
