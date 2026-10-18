using System.Text.Json;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwPermissionReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "etw-report-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void LatestAttemptIsBoundedAndContainsOnlyAllowlistedFields()
    {
        Assert.True(EtwPermissionReport.SaveTo(_directory, EtwPermissionAction.Add, new(EtwPermissionOutcome.AccessDenied, 5)));
        Assert.True(EtwPermissionReport.SaveTo(_directory, EtwPermissionAction.Add, new(EtwPermissionOutcome.Cancelled)));
        var file = Assert.Single(Directory.GetFiles(_directory));
        var text = File.ReadAllText(file);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(new[] { "schema_version", "stage", "utc", "action", "outcome", "windows_error", "message", "windows_version" },
            json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Cancelled", json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("windows_error").ValueKind);
        foreach (var privateValue in new[] { Environment.UserName, Environment.MachineName, @"C:\Users\", "S-1-5-21-", "TarkovSkills.EtwSetup." })
            Assert.DoesNotContain(privateValue, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnwritableReportDoesNotThrowOrReplaceOtherFiles()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(file, "unchanged");
        Assert.False(EtwPermissionReport.SaveTo(file, EtwPermissionAction.Add, new(EtwPermissionOutcome.Failed)));
        Assert.Equal("unchanged", File.ReadAllText(file));
        Assert.False(EtwPermissionReport.SaveTo(_directory, (EtwPermissionAction)100, new(EtwPermissionOutcome.Added)));
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
