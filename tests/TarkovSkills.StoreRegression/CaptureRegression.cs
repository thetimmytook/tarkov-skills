namespace TarkovSkills.StoreRegression;

public sealed partial class RegressionSuite
{
    private async Task CaptureCases()
    {
        if (options.NoRaid)
        {
            await Case("CLI-DURATION-RANGE", async () =>
            {
                await RequireRaid(false);
                var output = await client.RunAsync(["capture", "--duration", "60"]);
                using var json = output.Json();
                Check.That(output.ExitCode == 1 && json.RootElement.GetProperty("status").GetString() == "failed" &&
                    json.RootElement.GetProperty("message").GetString()!.Contains("120 or 240"), "Unsupported capture duration was not rejected.");
                return "Unsupported duration rejected before capture.";
            });
            foreach (var duration in new[] { "120", "240" })
                await Case("CLI-NO-RAID-" + duration, async () =>
                {
                    await RequireRaid(false);
                    var output = await client.RunAsync(["capture", "--duration", duration], int.Parse(duration) + 45);
                    using var json = output.Json();
                    Check.That(output.ExitCode != 0 && json.RootElement.GetProperty("status").GetString() == "failed" &&
                        json.RootElement.GetProperty("message").GetString()!.Contains("raid", StringComparison.OrdinalIgnoreCase),
                        "Capture was not rejected for the raid precondition.");
                    return "Capture rejected with JSON and nonzero exit; no GUI.";
                });
        }
        if (options.Capture)
            await Case("CLI-CAPTURE", async () =>
            {
                await RequireRaid(true);
                Console.WriteLine($"Capturing with bundled PresentMon for {options.Duration} seconds; no upload.");
                var output = await client.RunAsync(["capture", "--duration", options.Duration.ToString()], options.Duration + 45);
                CaptureAssertions.Complete(output, options.Duration);
                return $"Complete {options.Duration}s capture; sanitized JSON with metrics/context. No GUI history or upload expected.";
            });
        else cases.Add(new("CLI-CAPTURE", "NOT RUN", "Needs an active raid and explicit --capture."));
        cases.Add(new("CLI-CANCEL/RAID-END", "NOT RUN", "Separate live scenarios: Ctrl+C or manual raid exit; never simulated by killing the game."));
        cases.Add(new("BENCHMARK-COLLECT", "NOT RUN", "Legacy standalone command opens GUI and requires manual Start; not a headless CLI test."));
    }

    private async Task RequireRaid(bool expected)
    {
        var output = await client.RunAsync(["status"]);
        using var json = output.Json();
        var root = json.RootElement;
        if (output.ExitCode != 0 || !root.GetProperty("presentmon_ready").GetBoolean())
            throw new TestBlocked("Bundled capture dependency is not ready.");
        if (root.GetProperty("raid_active").GetBoolean() != expected)
            throw new TestBlocked(expected ? "Enter a raid before the live test." : "Leave the raid before the negative test.");
    }
}
