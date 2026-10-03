using System.Text.Json;
using TarkovSkills.StoreRegression;

namespace TarkovSkills.Regression.Tests;

public sealed class RunnerContractTests
{
    [Fact]
    public void DefaultModeDoesNotMutateGoalOrCapture()
    {
        var options = RunnerOptions.Parse([]);
        Assert.False(options.GoalWrite);
        Assert.False(options.Capture);
        Assert.False(options.NoRaid);
        Assert.Null(options.ReportPath);
    }

    [Theory]
    [InlineData("--duration", "60")]
    [InlineData("--duration", "invalid")]
    [InlineData("--report")]
    [InlineData("--unknown")]
    [InlineData("--no-raid", "--capture")]
    public void UnsafeOrIncompleteArgumentsAreRejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(args));

    [Fact]
    public void GoalArgumentsPreserveSpacesAndNotes()
    {
        var values = new GoalValues("stable fps", 65, "balanced quality", "notes with spaces");
        Assert.Equal(["goal", "set", "--goal", "stable fps", "--target-fps", "65", "--quality", "balanced quality", "--notes", "notes with spaces"], values.Arguments());
    }

    [Theory]
    [InlineData("PASS", 0)]
    [InlineData("NOT RUN", 0)]
    [InlineData("FAIL", 1)]
    [InlineData("BLOCKED", 2)]
    public void ExitCodeDoesNotTreatBlockedAsPass(string status, int expected) =>
        Assert.Equal(expected, new RegressionReport(DateTimeOffset.UtcNow, [new("case", status, "detail")]).ExitCode);

    [Theory]
    [InlineData("{}\n{}")]
    [InlineData("log line\n{}")]
    [InlineData("[]")]
    public void JsonRequiresExactlyOneObject(string output) =>
        Assert.Throws<TestFailure>(() => new CommandOutput(0, output, "").Json());

    [Theory]
    [InlineData("{\"system\":{\"os\":{\"version\":\"10.0.19045.0\"},\"gpu\":[{\"driver_version\":\"32.0.15.7628\"}]},\"raid\":{\"game_version\":\"0.16.9.0\"}}", false)]
    [InlineData("{\"notes\":\"connect to 192.168.1.20\"}", true)]
    [InlineData("{\"notes\":\"connect to 2001:db8::1\"}", true)]
    [InlineData("{\"notes\":\"C:\\\\Users\\\\someone\\\\file\"}", true)]
    [InlineData("{\"serial_number\":\"not public\"}", true)]
    [InlineData("{\"notes\":\"hostname SOMEHOST\"}", true)]
    public void PrivacyAuditDistinguishesVersionsFromPrivateData(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, PrivacyAudit.Findings(document.RootElement, "someuser", "SOMEHOST").Count > 0);
    }
}
