using Microsoft.Win32;
using System.Globalization;
using System.Management;
using System.Text.Json.Serialization;

namespace TarkovSkills.Core;

public sealed record GoalState(
    [property: JsonPropertyName("goal")] string Goal,
    [property: JsonPropertyName("target_fps_min")] int TargetFpsMin,
    [property: JsonPropertyName("quality_preference")] string QualityPreference,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("source")] string Source);

public sealed record InspectionReport(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("tarkov_running")] bool TarkovRunning,
    [property: JsonPropertyName("raid")] RaidContext Raid,
    [property: JsonPropertyName("goal")] GoalState Goal,
    [property: JsonPropertyName("system")] object System,
    [property: JsonPropertyName("settings")] object Settings,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

public sealed record CaptureReport(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("inspection")] InspectionReport Inspection,
    [property: JsonPropertyName("performance")] PerformanceMetrics Performance);

public sealed class GoalStore
{
    private readonly string _path;

    public GoalStore(string? path = null) => _path = path ?? Path.Combine(AppPaths.DataDirectory, "memory", "current-goal.json");

    public GoalState Load()
    {
        if (!File.Exists(_path)) return Default();
        try { return System.Text.Json.JsonSerializer.Deserialize<GoalState>(File.ReadAllText(_path), JsonDefaults.Options) ?? Default(); }
        catch (System.Text.Json.JsonException) { return Default(); }
    }

    public GoalState Save(string goal, int targetFpsMin, string qualityPreference, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Goal is required.", nameof(goal));
        if (targetFpsMin is < 20 or > 360) throw new ArgumentOutOfRangeException(nameof(targetFpsMin), "Target FPS must be between 20 and 360.");
        if (string.IsNullOrWhiteSpace(qualityPreference)) throw new ArgumentException("Quality preference is required.", nameof(qualityPreference));
        var value = new GoalState(goal.Trim(), targetFpsMin, qualityPreference.Trim(), string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), DateTimeOffset.Now, "local-memory");
        AppPaths.AtomicWrite(_path, System.Text.Json.JsonSerializer.Serialize(value, JsonDefaults.Options));
        return value;
    }

    public static GoalState Default() => new("stable-fps", 60, "balanced visibility/performance", "Default target: stable playable 50-60 FPS minimum when realistic.", null, "default");
}

public sealed class InspectionService
{
    public InspectionReport Inspect()
    {
        using var process = TarkovState.FindProcess();
        DateTime? startedAt = null;
        if (process is not null) { try { startedAt = process.StartTime; } catch { } }
        var settings = new SettingsReader().Read();
        var system = new SystemInfoCollector().Collect();
        RaidContext raid;
        try { raid = new RaidLogReader().Read(startedAt); }
        catch (Exception) { raid = new(false, false, "unknown", "unknown", null, null, null); system.Warnings.Add("Tarkov logs could not be read."); }
        var goal = PrivacySanitizer.Sanitize(new GoalStore().Load());
        return new InspectionReport(1, DateTimeOffset.Now, process is not null, raid, goal, system.System, settings.Settings, settings.Warnings.Concat(system.Warnings).Distinct().ToList());
    }
}

public sealed class CaptureDiscardedException(string message) : Exception(message);

public sealed class FrametimeCaptureService
{
    private static readonly TimeSpan RaidPollInterval = TimeSpan.FromSeconds(4);

    public async Task<CaptureReport> CaptureAsync(int durationSeconds, Action started, CancellationToken cancellationToken)
    {
        if (durationSeconds is not (120 or 240)) throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Capture duration must be 120 or 240 seconds.");
        using var process = TarkovState.FindProcess() ?? throw new InvalidOperationException("Run Tarkov and enter a raid before starting capture.");
        DateTime? processStartedAt = null; try { processStartedAt = process.StartTime; } catch { }
        var logs = new RaidLogReader();
        var raid = logs.Read(processStartedAt);
        if (!raid.Active || !raid.StartedAt.HasValue) throw new InvalidOperationException("Enter a raid before starting capture.");

        var inspection = new InspectionService().Inspect();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var raidEnded = 0;
        var monitor = MonitorRaidEndAsync(logs, raid.StartedAt.Value, () => { Interlocked.Exchange(ref raidEnded, 1); linked.Cancel(); }, linked.Token);
        try
        {
            var metrics = await new PresentMonRunner().CaptureAsync(durationSeconds, started, linked.Token);
            var finalContext = logs.Read(processStartedAt);
            CaptureValidation.EnsureComplete(metrics, finalContext, HasExited(process));
            return new CaptureReport(1, DateTimeOffset.Now, inspection, metrics);
        }
        catch (OperationCanceledException) when (Volatile.Read(ref raidEnded) == 1)
        {
            throw new CaptureDiscardedException("The raid ended before capture completed. Partial data was discarded.");
        }
        catch (Exception) when (HasExited(process))
        {
            throw new CaptureDiscardedException("Tarkov closed before capture completed. Partial data was discarded.");
        }
        finally
        {
            linked.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }
    }

    private static async Task MonitorRaidEndAsync(RaidLogReader logs, DateTime startedAt, Action ended, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(RaidPollInterval, cancellationToken).ConfigureAwait(false);
            try { if (logs.HasRaidEndedSince(startedAt)) { ended(); return; } }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool HasExited(System.Diagnostics.Process process) { try { return process.HasExited; } catch { return true; } }
}

public static class CaptureValidation
{
    public static void EnsureComplete(PerformanceMetrics metrics, RaidContext context, bool tarkovExited)
    {
        if (tarkovExited) throw new CaptureDiscardedException("Tarkov closed before the measurement completed. The partial result was discarded.");
        if (!context.Active) throw new CaptureDiscardedException("The raid ended before the measurement completed. The partial result was discarded.");
        if (metrics.DurationSec < 110) throw new CaptureDiscardedException($"Only {metrics.DurationSec:0.0} seconds of valid frametime data were captured. The partial result was discarded.");
    }
}

public static class PrivacySanitizer
{
    public static GoalState Sanitize(GoalState goal) => goal with
    {
        Goal = SanitizeText(goal.Goal),
        QualityPreference = SanitizeText(goal.QualityPreference),
        Notes = goal.Notes is null ? null : SanitizeText(goal.Notes)
    };

    public static string SanitizeText(string value)
    {
        var result = value;
        foreach (var identity in new[] { Environment.UserName, Environment.MachineName }.Where(item => !string.IsNullOrWhiteSpace(item)))
            result = System.Text.RegularExpressions.Regex.Replace(result, $@"(?<![A-Za-z0-9]){System.Text.RegularExpressions.Regex.Escape(identity)}(?![A-Za-z0-9])", "user", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?i)\b[A-Z]:\\[^\r\n\""']+", "<local-path>");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?i)(Users[\\/])[^\\/\""']+", "$1user");
        return result;
    }
}

public static class HardwareInfo
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static IReadOnlyDictionary<string, double> ReadGpuVramGb()
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (root is null) return result;
            foreach (var childName in root.GetSubKeyNames().Where(name => name.Length == 4 && name.All(char.IsDigit)))
            {
                using var child = root.OpenSubKey(childName);
                var name = child?.GetValue("DriverDesc")?.ToString();
                var raw = child?.GetValue("HardwareInformation.qwMemorySize") ?? child?.GetValue("HardwareInformation.MemorySize");
                if (string.IsNullOrWhiteSpace(name) || !TryUInt64(raw, out var bytes) || bytes == 0 || result.ContainsKey(name)) continue;
                result[name] = Math.Round(bytes / 1073741824d, 2);
            }
        }
        catch { }
        return result;
    }

    public static string? FindTarkovInstallLocation()
    {
        foreach (var keyPath in new[] { @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890" })
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            var path = key?.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) return path;
        }
        return null;
    }

    public static string GetDriveMediaType(string? path)
    {
        var root = string.IsNullOrWhiteSpace(path) ? null : Path.GetPathRoot(path);
        var letter = root?.TrimEnd('\\').TrimEnd(':');
        if (string.IsNullOrWhiteSpace(letter)) return "unknown";
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var partitionSearch = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT DriveLetter,DiskNumber FROM MSFT_Partition"));
            var partition = partitionSearch.Get().Cast<ManagementBaseObject>().FirstOrDefault(item => string.Equals(item["DriveLetter"]?.ToString(), letter, StringComparison.OrdinalIgnoreCase));
            if (partition is null) return "unknown";
            var diskNumber = Convert.ToUInt32(partition["DiskNumber"], CultureInfo.InvariantCulture);
            using var diskSearch = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT DeviceId,MediaType FROM MSFT_PhysicalDisk"));
            var disk = diskSearch.Get().Cast<ManagementBaseObject>().FirstOrDefault(item => Convert.ToUInt32(item["DeviceId"] ?? uint.MaxValue, CultureInfo.InvariantCulture) == diskNumber);
            return Convert.ToInt32(disk?["MediaType"] ?? 0, CultureInfo.InvariantCulture) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "unknown" };
        }
        catch { return "unknown"; }
    }

    private static bool TryUInt64(object? value, out ulong result)
    {
        try { result = Convert.ToUInt64(value, CultureInfo.InvariantCulture); return true; }
        catch { result = 0; return false; }
    }
}
