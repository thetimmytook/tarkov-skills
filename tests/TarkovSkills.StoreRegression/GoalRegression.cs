using System.Text.Json;

namespace TarkovSkills.StoreRegression;

public sealed partial class RegressionSuite
{
    private async Task<GoalValues> ReadGoal()
    {
        var output = await client.RunAsync(["goal", "get"]);
        using var json = output.Json();
        Check.That(output.ExitCode == 0, "Goal get failed.");
        return GoalValues.Read(json.RootElement);
    }

    private async Task GoalWriteCases()
    {
        GoalValues original;
        try { original = await ReadGoal(); }
        catch { cases.Add(new("CLI-GOAL-WRITE", "BLOCKED", "Cannot back up Goal values; no mutation attempted.")); return; }
        var testName = "regression-" + Guid.NewGuid().ToString("N");
        var mutationAttempted = false;
        try
        {
            foreach (var fps in new[] { 60, 65, 20, 360 })
                await Case("CLI-GOAL-WRITE-" + fps, async () =>
                {
                    var expected = new GoalValues(testName, fps, "test quality with spaces", "Temporary regression test");
                    mutationAttempted = true;
                    var output = await client.RunAsync(expected.Arguments());
                    using var json = output.Json();
                    Check.That(output.ExitCode == 0 && GoalValues.Read(json.RootElement) == expected, "Goal set result differs.");
                    Check.That(await ReadGoal() == expected, "Goal did not survive a new CLI process.");
                    return "Values and quoted arguments persisted across CLI processes.";
                });
            // Even invalid setters are opt-in: a future validation regression could write state.
            if (mutationAttempted)
            {
                await ErrorCase("CLI-GOAL-MISSING", ["goal", "set"], 1, "failed");
                await ErrorCase("CLI-GOAL-TEXT-FPS", ["goal", "set", "--goal", testName, "--target-fps", "invalid", "--quality", "test"], 1, "failed");
                foreach (var fps in new[] { "19", "361" })
                    await ErrorCase("CLI-GOAL-RANGE-" + fps, ["goal", "set", "--goal", testName, "--target-fps", fps, "--quality", "test"], 1, "failed");
                await ErrorCase("CLI-GOAL-QUALITY", ["goal", "set", "--goal", testName, "--target-fps", "60"], 1, "failed");
            }
        }
        finally
        {
            if (mutationAttempted)
                await Case("CLI-GOAL-RESTORE", async () =>
                {
                    var current = await ReadGoal();
                    if (current == original) return "Original Goal values already present.";
                    Check.That(current.Name == testName, "Goal changed outside the runner; restoration refused to preserve that edit.");
                    var restored = await client.RunAsync(original.Arguments());
                    Check.That(restored.ExitCode == 0 && await ReadGoal() == original, "Original Goal values could not be restored.");
                    return "Original values restored through alias; updated_at/source metadata may differ.";
                });
        }
    }
}

public sealed record GoalValues(string Name, int Fps, string Quality, string? Notes)
{
    public static GoalValues Read(JsonElement root) => new(root.GetProperty("goal").GetString()!,
        root.GetProperty("target_fps_min").GetInt32(), root.GetProperty("quality_preference").GetString()!,
        root.GetProperty("notes").ValueKind == JsonValueKind.Null ? null : root.GetProperty("notes").GetString());

    public string[] Arguments()
    {
        List<string> args = ["goal", "set", "--goal", Name, "--target-fps", Fps.ToString(System.Globalization.CultureInfo.InvariantCulture), "--quality", Quality];
        if (Notes is not null) args.AddRange(["--notes", Notes]);
        return args.ToArray();
    }
}
