using System.Text.Json;
using TarkovSkills.Core;

namespace TarkovSkills.Core.Tests;

public sealed class InspectionPrivacyTests
{
    [Fact]
    public void SanitizerMasksIdentityAndAbsolutePath()
    {
        var input = $@"{Environment.UserName} on {Environment.MachineName}: C:\Users\{Environment.UserName}\Desktop\capture.csv";
        var sanitized = PrivacySanitizer.SanitizeText(input);
        Assert.DoesNotContain(Environment.UserName, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InspectionDoesNotExposeLocalIdentityOrPaths()
    {
        var json = JsonSerializer.Serialize(new InspectionService().Inspect(), JsonDefaults.Options);
        Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings_dir", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("install_location", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Control.ini", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sound.ini", json, StringComparison.OrdinalIgnoreCase);
    }
}
