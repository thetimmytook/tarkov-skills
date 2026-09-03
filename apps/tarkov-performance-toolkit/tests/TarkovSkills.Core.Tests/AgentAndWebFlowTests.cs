using System.Text.Json;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovSkills.Core.Tests;

public sealed class AgentAndWebFlowTests
{
    [Fact]
    public async Task AgentInspectEmitsSanitizedJsonWithoutWpfDependency()
    {
        using var output = new StringWriter();

        var exitCode = await new ToolkitCli(output).RunAsync(["inspect"]);

        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, document.RootElement.GetProperty("schema_version").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("system", out _));
        Assert.True(document.RootElement.TryGetProperty("settings", out _));
        Assert.DoesNotContain(Environment.UserName, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, output.ToString(), StringComparison.OrdinalIgnoreCase);

        var references = typeof(ToolkitCli).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("TarkovPerformanceToolkit", references);
        Assert.DoesNotContain("TarkovBenchmark.Feature", references);
    }

    [Fact]
    public async Task AgentCommandFailureIsMachineReadableAndNonzero()
    {
        using var output = new StringWriter();

        var exitCode = await new ToolkitCli(output).RunAsync(["capture", "--duration", "30"]);

        Assert.NotEqual(0, exitCode);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
        Assert.Contains("120 or 240", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void WebProfileShowsCopyResultsAndCopiesOnlyLatestRun()
    {
        var options = BenchmarkFeatureOptions.ForToolkit("1.0.0");

        var json = BenchmarkSubmission.SerializeLatest([Run("first"), Run("latest")]);

        Assert.True(options.ShowCopyResults);
        using var document = JsonDocument.Parse(json);
        var runs = document.RootElement.GetProperty("runs");
        Assert.Equal(1, runs.GetArrayLength());
        Assert.Equal("latest", runs[0].GetProperty("run_id").GetString());
    }

    [Fact]
    public void WebCopyRejectsMissingCompletedRun()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BenchmarkSubmission.SerializeLatest([]));
        Assert.Contains("no completed benchmark", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BenchmarkRun Run(string id) => new(
        id,
        "2026-09-03",
        120,
        "1.0.0",
        new { cpu = "test" },
        new { resolution = "1920x1080" },
        new { map = "Factory" },
        new PerformanceMetrics(12000, 120, 100, 70, 50, 10, 15, 20),
        []);
}
