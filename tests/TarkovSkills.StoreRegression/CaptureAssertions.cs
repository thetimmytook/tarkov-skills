using System.Text.Json;

namespace TarkovSkills.StoreRegression;

public static class CaptureAssertions
{
    public static void Audit(JsonElement root) => Check.That(
        PrivacyAudit.Findings(root, Environment.UserName, Environment.MachineName).Count == 0,
        "Privacy audit failed; raw output is not retained.");

    public static void CheckAvailability(CommandOutput output)
    {
        if (output.ExitCode is 10 or 11)
        {
            using var json = output.Json();
            var expected = output.ExitCode == 10 ? "permission_required" : "capture_conflict";
            Check.That(json.RootElement.GetProperty("status").GetString() == expected,
                "Capture error exit code and JSON status disagree.");
            throw new TestBlocked("Capture needs ETW permissions or another capture must finish; no automatic elevation or external-session cleanup.");
        }
    }

    public static void Complete(CommandOutput output, int duration)
    {
        CheckAvailability(output);
        using var json = output.Json();
        Check.That(output.ExitCode == 0, "Live capture failed or was discarded.");
        Audit(json.RootElement);
        Check.That(json.RootElement.TryGetProperty("inspection", out _), "Capture context is missing.");
        var performance = json.RootElement.GetProperty("performance");
        var seconds = performance.GetProperty("duration_sec").GetDouble();
        Check.That(double.IsFinite(seconds) && seconds >= duration - 10 && seconds <= duration + 10 &&
            performance.GetProperty("sample_count").GetInt32() > 0, "Capture duration/sample count is incomplete.");
        foreach (var metric in new[] { "average_fps", "one_percent_low_fps", "zero_point_one_percent_low_fps",
            "average_frametime_ms", "p95_frametime_ms", "p99_frametime_ms" })
            Check.That(double.IsFinite(performance.GetProperty(metric).GetDouble()) &&
                performance.GetProperty(metric).GetDouble() >= 0, "Invalid performance metric.");
        Check.That(performance.GetProperty("average_fps").GetDouble() > 0 &&
            performance.GetProperty("average_frametime_ms").GetDouble() > 0 &&
            performance.GetProperty("one_percent_low_fps").GetDouble() <= performance.GetProperty("average_fps").GetDouble() &&
            performance.GetProperty("zero_point_one_percent_low_fps").GetDouble() <= performance.GetProperty("one_percent_low_fps").GetDouble() &&
            performance.GetProperty("p95_frametime_ms").GetDouble() <= performance.GetProperty("p99_frametime_ms").GetDouble(),
            "Performance metrics are inconsistent.");
    }

    public static void Discarded(CommandOutput output)
    {
        CheckAvailability(output);
        using var json = output.Json();
        Audit(json.RootElement);
        Check.That(output.ExitCode == 12 && json.RootElement.GetProperty("status").GetString() == "discarded",
            "Raid-end capture did not return exit 12 with discarded status.");
        Check.That(!json.RootElement.TryGetProperty("performance", out _) &&
            !json.RootElement.TryGetProperty("inspection", out _), "Discarded capture exposed partial results.");
        Check.That(json.RootElement.GetProperty("message").GetString()?.Contains("raid", StringComparison.OrdinalIgnoreCase) == true,
            "Capture was discarded for a different reason, not raid end.");
    }
}
