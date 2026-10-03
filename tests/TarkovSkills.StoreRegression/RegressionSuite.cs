using System.Text.Json;

namespace TarkovSkills.StoreRegression;

public sealed partial class RegressionSuite(RunnerOptions options)
{
    private readonly List<CaseResult> cases = [];
    private StoreAliasClient client = null!;

    public async Task<RegressionReport> RunAsync()
    {
        await Case("CLI-ALIAS", () =>
        {
            client = new StoreAliasClient();
            return Task.FromResult($"Microsoft-signed x64 Toolkit {client.Version}; registered Store alias.");
        });
        if (client is null) return new(DateTimeOffset.UtcNow, cases);
        var guiBefore = StoreAliasClient.GuiProcessCount();
        await Case("CLI-STATUS", async () =>
        {
            var output = await client.RunAsync(["status"]);
            using var json = output.Json();
            var root = json.RootElement;
            Check.That(output.ExitCode == 0 && root.GetProperty("status").GetString() == "ok" &&
                root.GetProperty("presentmon_ready").GetBoolean(), "Dependency/status is not ready.");
            Check.That(root.GetProperty("toolkit_version").GetString() == client.Version, "Alias version differs from package.");
            var inRaid = root.GetProperty("raid_active").GetBoolean();
            var running = root.GetProperty("tarkov_running").GetBoolean();
            Check.That(!inRaid || running, "Active raid reported while game is closed.");
            return $"One JSON document; game running={running}, active raid={inRaid}.";
        });
        await Case("CLI-INSPECT", async () =>
        {
            var output = await client.RunAsync(["inspect"]);
            using var json = output.Json();
            Check.That(output.ExitCode == 0, "Inspect returned a failure exit code.");
            foreach (var field in new[] { "system", "settings", "raid", "goal", "warnings" })
                Check.That(json.RootElement.TryGetProperty(field, out _), "Inspection contract is incomplete.");
            foreach (var field in new[] { "graphics", "postfx", "game" })
                Check.That(json.RootElement.GetProperty("settings").TryGetProperty(field, out _), "Settings contract is incomplete.");
            Audit(json.RootElement);
            return "Inspection contract and privacy checks passed; raw report is not retained.";
        });
        await Case("CLI-GOAL-GET", async () =>
        {
            var goal = await ReadGoal();
            var output = await client.RunAsync(["inspect"]);
            using var json = output.Json();
            Check.That(goal == GoalValues.Read(json.RootElement.GetProperty("goal")), "Goal differs between get and inspect.");
            return "Goal get/inspect agree.";
        });
        foreach (var helpArgs in new[] { Array.Empty<string>(), new[] { "help" }, new[] { "--help" }, new[] { "-h" } })
            await Case("CLI-HELP-" + (helpArgs.FirstOrDefault() ?? "default"), async () =>
            {
                var output = await client.RunAsync(helpArgs);
                Check.That(output.ExitCode == 0 && string.IsNullOrWhiteSpace(output.Stderr) &&
                    output.Stdout.Contains("capture") && output.Stdout.Contains("goal set"), "Help contract failed.");
                return "Help intentionally returns text, not JSON.";
            });
        await ErrorCase("CLI-UNKNOWN", ["regression-invalid-command"], 2, "unknown_command");
        await ErrorCase("CLI-GOAL-UNKNOWN", ["goal", "regression-invalid"], 2, "invalid_goal_command");
        if (options.GoalWrite) await GoalWriteCases();
        else cases.Add(new("CLI-GOAL-WRITE", "NOT RUN", "Opt in with --goal-write; saved values are restored, metadata can change."));
        await CaptureCases();
        await Case("CLI-NO-GUI", () =>
        {
            Check.That(StoreAliasClient.GuiProcessCount() == guiBefore, "Store GUI process count changed during CLI checks.");
            return Task.FromResult("Both Store GUI process counts unchanged (no interaction with existing windows).");
        });
        return new(DateTimeOffset.UtcNow, cases);
    }

    private async Task Case(string id, Func<Task<string>> action)
    {
        Console.WriteLine($"RUN      {id}");
        try { cases.Add(new(id, "PASS", await action())); }
        catch (TestBlocked exception) { cases.Add(new(id, "BLOCKED", exception.Message)); }
        catch (TestFailure exception) { cases.Add(new(id, "FAIL", exception.Message)); }
        catch (Exception exception) { cases.Add(new(id, "FAIL", $"Unexpected {exception.GetType().Name}; raw diagnostics withheld.")); }
    }

    private Task ErrorCase(string id, string[] args, int exit, string status) => Case(id, async () =>
    {
        var before = await ReadGoal();
        var output = await client.RunAsync(args);
        using var json = output.Json();
        Check.That(output.ExitCode == exit && json.RootElement.GetProperty("status").GetString() == status,
            "Expected machine-readable nonzero failure was not returned.");
        Check.That(await ReadGoal() == before, "Rejected command changed saved Goal.");
        return $"Exit {exit}, JSON status {status}; Goal unchanged.";
    });

    private static void Audit(JsonElement root) => Check.That(
        PrivacyAudit.Findings(root, Environment.UserName, Environment.MachineName).Count == 0,
        "Privacy audit failed; potentially private output is not retained.");
}
