using System.Diagnostics;
using System.Text.Json;
using TarkovSkills.Core;

return await new ToolkitCli(Console.Out).RunAsync(args);

public sealed class ToolkitCli(TextWriter output)
{
    private readonly TextWriter _output = output;

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0])) { WriteHelp(); return 0; }
            return args[0].ToLowerInvariant() switch
            {
                "status" => Status(),
                "inspect" => Inspect(),
                "goal" => Goal(args.Skip(1).ToArray()),
                "capture" => await CaptureAsync(args.Skip(1).ToArray()),
                _ => Fail("unknown_command", $"Unknown command: {args[0]}", 2)
            };
        }
        catch (PresentMonPermissionException ex) { return Fail("permission_required", ex.Message, 10); }
        catch (PresentMonSessionException ex) { return Fail("capture_conflict", ex.Message, 11); }
        catch (CaptureDiscardedException ex) { return Fail("discarded", ex.Message, 12); }
        catch (OperationCanceledException) { return Fail("cancelled", "Capture was cancelled. Partial data was discarded.", 13); }
        catch (Exception ex) { return Fail("failed", ex.Message, 1); }
    }

    private int Status()
    {
        using var process = TarkovState.FindProcess();
        var presentMon = new PresentMonRunner();
        var ready = presentMon.IsDependencyReady(out var dependencyMessage);
        RaidContext raid;
        try { raid = new RaidLogReader().Read(TryStartTime(process)); }
        catch { raid = new(false, false, "unknown", "unknown", null, null, null); }
        Write(new { status = ready ? "ok" : "not_ready", toolkit_version = typeof(ToolkitCli).Assembly.GetName().Version?.ToString(3), presentmon_ready = ready, presentmon_status = dependencyMessage, tarkov_running = process is not null, raid_active = raid.Active, map = raid.Map });
        return ready ? 0 : 3;
    }

    private int Inspect() { Write(new InspectionService().Inspect()); return 0; }

    private int Goal(string[] args)
    {
        var store = new GoalStore();
        if (args.Length == 0 || args[0].Equals("get", StringComparison.OrdinalIgnoreCase)) { Write(store.Load()); return 0; }
        if (!args[0].Equals("set", StringComparison.OrdinalIgnoreCase)) return Fail("invalid_goal_command", "Use goal get or goal set.", 2);
        var goal = Option(args, "--goal") ?? throw new ArgumentException("--goal is required.");
        var targetText = Option(args, "--target-fps") ?? throw new ArgumentException("--target-fps is required.");
        if (!int.TryParse(targetText, out var target)) throw new ArgumentException("--target-fps must be an integer.");
        var quality = Option(args, "--quality") ?? throw new ArgumentException("--quality is required.");
        Write(store.Save(goal, target, quality, Option(args, "--notes")));
        return 0;
    }

    private async Task<int> CaptureAsync(string[] args)
    {
        var duration = int.TryParse(Option(args, "--duration"), out var parsed) ? parsed : 120;
        if (duration is not (120 or 240)) throw new ArgumentException("Capture duration must be 120 or 240 seconds.");
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Write(await new FrametimeCaptureService().CaptureAsync(duration, () => { }, cancellation.Token));
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static DateTime? TryStartTime(Process? process) { try { return process?.StartTime; } catch { return null; } }
    private static bool IsHelp(string value) => value is "help" or "--help" or "-h" or "/?";
    private void Write(object value) => _output.WriteLine(JsonSerializer.Serialize(value, JsonDefaults.Options));
    private int Fail(string status, string message, int exitCode) { Write(new { status, message }); return exitCode; }
    private void WriteHelp() => _output.WriteLine("Tarkov Performance Toolkit\n\nCommands:\n  status\n  inspect\n  capture [--duration 120|240]\n  goal get\n  goal set --goal <name> --target-fps <20-360> --quality <text> [--notes <text>]");
}
