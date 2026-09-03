using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core;

public static class TarkovState
{
    public static Process? FindProcess() => Process.GetProcessesByName("EscapeFromTarkov").FirstOrDefault();
}

public sealed class SettingsReader
{
    public (object Settings, List<string> Warnings) Read()
    {
        var warnings = new List<string>();
        var result = new Dictionary<string, object?>();
        foreach (var item in new[] { ("graphics", "Graphics.ini"), ("postfx", "PostFx.ini"), ("game", "Game.ini") })
        {
            var path = Path.Combine(AppPaths.TarkovSettingsDirectory, item.Item2);
            try { result[item.Item1] = File.Exists(path) ? Parse(path) : null; if (!File.Exists(path)) warnings.Add($"{item.Item2} was not found."); }
            catch (Exception) { result[item.Item1] = null; warnings.Add($"{item.Item2} could not be read."); }
        }
        return (result, warnings);
    }

    private static object? Parse(string path)
    {
        var text = File.ReadAllText(path).Trim();
        if (text.StartsWith('{') || text.StartsWith('[')) return JsonNode.Parse(text);
        var root = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase) { ["root"] = new(StringComparer.OrdinalIgnoreCase) };
        var section = "root";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim(); if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; root.TryAdd(section, new(StringComparer.OrdinalIgnoreCase)); continue; }
            var separator = line.IndexOf('='); if (separator > 0) root[section][line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return root;
    }
}

public sealed class SystemInfoCollector
{
    public (object System, List<string> Warnings) Collect()
    {
        var warnings = new List<string>();
        var vram = HardwareInfo.ReadGpuVramGb();
        object? cpu = QueryOne("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor", o => new { name = Text(o, "Name"), cores = Number(o, "NumberOfCores"), logical_processors = Number(o, "NumberOfLogicalProcessors"), max_clock_mhz = Number(o, "MaxClockSpeed") }, warnings);
        object? os = QueryOne("SELECT Caption,Version,BuildNumber,OSArchitecture FROM Win32_OperatingSystem", o => new { caption = Text(o, "Caption"), version = Text(o, "Version"), build = Text(o, "BuildNumber"), architecture = Text(o, "OSArchitecture") }, warnings);
        var gpu = QueryMany("SELECT Name,AdapterRAM,DriverVersion,CurrentHorizontalResolution,CurrentVerticalResolution FROM Win32_VideoController", o =>
        {
            var name = Text(o, "Name") ?? "unknown";
            var vendor = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ? "nvidia"
                : name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ? "amd"
                : name.Contains("Intel", StringComparison.OrdinalIgnoreCase) || name.Contains("Arc", StringComparison.OrdinalIgnoreCase) ? "intel" : "unknown";
            var registryVram = vram.GetValueOrDefault(name);
            var fallbackVram = Number(o, "AdapterRAM") > 0 ? Math.Round(Number(o, "AdapterRAM") / 1073741824d, 2) : 0;
            return new { name, vendor, vram_gb = registryVram > 0 ? registryVram : fallbackVram > 0 ? fallbackVram : (double?)null, vram_source = registryVram > 0 ? "registry" : fallbackVram > 0 ? "wmi_capped_4gb" : "unknown", driver_version = Text(o, "DriverVersion"), driver_latest_check = "manual", current_resolution = Resolution(o) };
        }, warnings);
        var modules = QueryMany("SELECT Manufacturer,Capacity,Speed,ConfiguredClockSpeed FROM Win32_PhysicalMemory", o => new { manufacturer = Text(o, "Manufacturer"), capacity_gb = Math.Round(Number(o, "Capacity") / 1073741824d, 2), speed_mhz = Number(o, "Speed"), configured_clock_speed_mhz = Number(o, "ConfiguredClockSpeed") }, warnings);
        var pagefiles = QueryMany("SELECT Name,AllocatedBaseSize,CurrentUsage,PeakUsage FROM Win32_PageFileUsage", o => new { drive_media_type = HardwareInfo.GetDriveMediaType(Text(o, "Name")), allocated_gb = Math.Round(Number(o, "AllocatedBaseSize") / 1024d, 2), current_usage_gb = Math.Round(Number(o, "CurrentUsage") / 1024d, 2), peak_usage_gb = Math.Round(Number(o, "PeakUsage") / 1024d, 2) }, warnings);
        var totalRam = modules.Sum(m => Convert.ToDouble(m.GetType().GetProperty("capacity_gb")!.GetValue(m), CultureInfo.InvariantCulture));
        var totalPagefile = pagefiles.Sum(item => Convert.ToDouble(item.GetType().GetProperty("allocated_gb")!.GetValue(item), CultureInfo.InvariantCulture));
        var install = HardwareInfo.FindTarkovInstallLocation();
        return (new { source = "windows_management", captured_at = DateTimeOffset.Now, os, cpu, gpu, ram = new { total_gb = Math.Round(totalRam, 2), modules }, pagefile = new { total_allocated_gb = Math.Round(totalPagefile, 2), files = pagefiles }, game_install = new { detected = install is not null, drive_media_type = HardwareInfo.GetDriveMediaType(install) } }, warnings);
    }

    private static object? QueryOne(string query, Func<ManagementBaseObject, object> map, List<string> warnings) { try { using var searcher = new ManagementObjectSearcher(query); return searcher.Get().Cast<ManagementBaseObject>().Select(map).FirstOrDefault(); } catch (Exception) { warnings.Add("System information was partially unavailable."); return null; } }
    private static List<object> QueryMany(string query, Func<ManagementBaseObject, object> map, List<string> warnings) { try { using var searcher = new ManagementObjectSearcher(query); return searcher.Get().Cast<ManagementBaseObject>().Select(map).ToList(); } catch (Exception) { warnings.Add("System information was partially unavailable."); return []; } }
    private static string? Text(ManagementBaseObject value, string name) => value[name]?.ToString()?.Trim();
    private static double Number(ManagementBaseObject value, string name) => Convert.ToDouble(value[name] ?? 0, CultureInfo.InvariantCulture);
    private static string? Resolution(ManagementBaseObject value) { var width = Number(value, "CurrentHorizontalResolution"); var height = Number(value, "CurrentVerticalResolution"); return width > 0 && height > 0 ? $"{width:0}x{height:0}" : null; }
}

public sealed class RaidLogReader
{
    private static readonly Regex Timestamp = new(@"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase) { ["TarkovStreets"] = "Streets of Tarkov", ["bigmap"] = "Customs", ["factory4_day"] = "Factory", ["factory4_night"] = "Factory", ["laboratory"] = "The Lab", ["Labyrinth"] = "Labyrinth", ["Lighthouse"] = "Lighthouse", ["RezervBase"] = "Reserve", ["Sandbox"] = "Ground Zero", ["Sandbox_high"] = "Ground Zero", ["Interchange"] = "Interchange", ["Shoreline"] = "Shoreline", ["Woods"] = "Woods" };
    private static readonly Dictionary<string, string> Bundles = new(StringComparer.OrdinalIgnoreCase) { ["city_preset"] = "TarkovStreets", ["customs_preset"] = "bigmap", ["factory_day_preset"] = "factory4_day", ["factory_night_preset"] = "factory4_night", ["laboratory_preset"] = "laboratory", ["labyrinth_preset"] = "Labyrinth", ["lighthouse_preset"] = "Lighthouse", ["rezerv_base_preset"] = "RezervBase", ["sandbox_preset"] = "Sandbox", ["sandbox_high_preset"] = "Sandbox_high", ["shopping_mall"] = "Interchange", ["shoreline_preset"] = "Shoreline", ["woods_preset"] = "Woods" };

    public RaidContext Read(DateTime? processStartedAt)
    {
        var logs = FindLogsDirectory(); if (logs is null) return new(false, false, "unknown", "unknown", null, null, null);
        var folders = Directory.EnumerateDirectories(logs).Select(x => new DirectoryInfo(x)).OrderByDescending(x => x.LastWriteTimeUtc).Take(5);
        foreach (var folder in folders)
        {
            var context = ParseFolder(folder, processStartedAt);
            if (context.Found) return context;
        }
        return new(true, false, "unknown", "unknown", null, null, null);
    }

    public bool HasRaidEndedSince(DateTime startedAt)
    {
        var logs = FindLogsDirectory();
        if (logs is null) return false;

        var folders = Directory.EnumerateDirectories(logs)
            .Select(x => new DirectoryInfo(x))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .Take(2);
        foreach (var folder in folders)
        {
            foreach (var file in folder.EnumerateFiles("*application*.log").OrderBy(x => x.LastWriteTimeUtc))
            {
                if (ContainsRaidEndMarkerAfter(ReadSharedLines(file.FullName), startedAt)) return true;
            }
        }
        return false;
    }

    internal static bool ContainsRaidEndMarkerAfter(IEnumerable<string> lines, DateTime startedAt)
    {
        foreach (var line in lines)
        {
            var time = ParseTime(line);
            if (time.HasValue && time.Value > startedAt && IsRaidEndMarker(line)) return true;
        }
        return false;
    }

    private static RaidContext ParseFolder(DirectoryInfo folder, DateTime? processStartedAt)
    {
        string mapId = "unknown"; DateTime? started = null; DateTime? ended = null; string? version = Regex.Match(folder.Name, @"_(?<v>\d+(\.\d+){3,4})$").Groups["v"].Value; if (version == "") version = null;
        foreach (var file in folder.EnumerateFiles("*.log").Where(x => Regex.IsMatch(x.Name, "application|backend|output", RegexOptions.IgnoreCase)).OrderBy(x => x.LastWriteTimeUtc))
        {
            foreach (var line in ReadSharedLines(file.FullName))
            {
                var time = ParseTime(line);
                var scene = Regex.Match(line, @"scene preset path:maps/(?<bundle>[a-zA-Z0-9_]+)\.bundle", RegexOptions.IgnoreCase); if (scene.Success) mapId = Bundles.GetValueOrDefault(scene.Groups["bundle"].Value, scene.Groups["bundle"].Value);
                var location = Regex.Match(line, @"TRACE-NetworkGameCreate profileStatus.*Location: (?<map>[^,]+)"); if (location.Success) mapId = location.Groups["map"].Value.Trim();
                if (line.Contains("application|GameStarted", StringComparison.OrdinalIgnoreCase)) { started = time; ended = null; }
                if (started.HasValue && IsRaidEndMarker(line) && (!time.HasValue || time.Value > started.Value)) ended = time ?? started;
            }
        }
        var validStart = started.HasValue && (!processStartedAt.HasValue || started.Value >= processStartedAt.Value.AddSeconds(-10));
        var active = validStart && (!ended.HasValue || ended.Value < started!.Value);
        return new(mapId != "unknown" || started.HasValue, active, MapNames.GetValueOrDefault(mapId, mapId), mapId, version, started, ended);
    }

    private static IEnumerable<string> ReadSharedLines(string path) { try { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var reader = new StreamReader(stream); while (reader.ReadLine() is { } line) yield return line; } finally { } }
    private static bool IsRaidEndMarker(string line) =>
        line.Contains("Got notification | UserMatchOver", StringComparison.OrdinalIgnoreCase)
        || line.Contains("PrepareSelectedProfileLocally ProfileId:", StringComparison.OrdinalIgnoreCase)
        || line.Contains("EFT.HideoutGameLoader:OnHideoutStart()", StringComparison.OrdinalIgnoreCase);
    private static DateTime? ParseTime(string line) { var match = Timestamp.Match(line); return match.Success && DateTime.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value) ? value : null; }
    private static string? FindLogsDirectory()
    {
        foreach (var keyPath in new[] { @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890" })
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath); var install = key?.GetValue("InstallLocation")?.ToString(); if (string.IsNullOrWhiteSpace(install)) continue;
            foreach (var candidate in new[] { Path.Combine(install, "Logs"), Path.Combine(install, "build", "Logs") }) if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}

public sealed class PresentMonPermissionException(string message) : Exception(message);
public sealed class PresentMonSessionException(string message) : Exception(message);

public sealed class PresentMonRunner
{
    internal const string OwnedSessionName = "TimmyTook.TarkovPerformanceBenchmark";

    public bool IsDependencyReady(out string message)
    {
        try { VerifyDependency(); message = "Bundled PresentMon 2.5.1 ready"; return true; }
        catch (Exception ex) { message = ex.Message; return false; }
    }

    public async Task<PerformanceMetrics> CaptureAsync(int durationSeconds, Action started, CancellationToken cancellationToken)
    {
        VerifyDependency();
        await CleanupLegacyOrphanedSessionAsync();
        await StopSessionAsync(OwnedSessionName);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TarkovBenchmark-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tempDirectory); var csv = Path.Combine(tempDirectory, "capture.csv");
        try
        {
            var info = new ProcessStartInfo(AppPaths.PresentMonFile) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in BuildArguments(durationSeconds, csv)) info.ArgumentList.Add(arg);
            using var process = new Process { StartInfo = info }; if (!process.Start()) throw new InvalidOperationException("PresentMon could not start."); started();
            using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken); var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken); var error = await stderr; _ = await stdout;
            if (process.ExitCode != 0) throw CreateExitException(process.ExitCode, error);
            if (!File.Exists(csv)) throw new InvalidOperationException("PresentMon did not create capture data.");
            return PresentMonCsvParser.Parse(csv);
        }
        finally
        {
            await StopSessionAsync(OwnedSessionName);
            try { Directory.Delete(tempDirectory, true); } catch { }
        }
    }

    internal static IReadOnlyList<string> BuildArguments(int durationSeconds, string outputFile) =>
    [
        "--session_name", OwnedSessionName,
        "--stop_existing_session",
        "--process_name", "EscapeFromTarkov.exe",
        "--timed", durationSeconds.ToString(CultureInfo.InvariantCulture),
        "--terminate_after_timed",
        "--terminate_on_proc_exit",
        "--no_console_stats",
        "--output_file", outputFile
    ];

    internal static Exception CreateExitException(int exitCode, string error)
    {
        if (error.Contains("already running", StringComparison.OrdinalIgnoreCase))
            return new PresentMonSessionException("Close the other performance capture and try again. No benchmark data was saved.");
        if (error.Contains("access denied", StringComparison.OrdinalIgnoreCase))
            return new PresentMonPermissionException("Windows denied access to the performance trace session.");
        return new InvalidOperationException($"PresentMon could not complete the measurement (exit code {exitCode}).");
    }

    internal static async Task CleanupLegacyOrphanedSessionAsync()
    {
        if (HasRunningPresentMonProcess())
            throw new PresentMonSessionException("Another PresentMon capture is running. Stop it, then try again. The other process was not changed.");
        if (!await SessionExistsAsync("PresentMon")) return;
        if (HasRunningPresentMonProcess())
            throw new PresentMonSessionException("Another PresentMon capture started during preparation. Stop it, then try again. The other process was not changed.");
        await StopSessionAsync("PresentMon");
    }

    private static bool HasRunningPresentMonProcess()
    {
        var processes = Process.GetProcessesByName("PresentMon");
        try { return processes.Any(process => !process.HasExited); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static async Task<bool> SessionExistsAsync(string sessionName)
    {
        var info = new ProcessStartInfo("logman.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "query", sessionName, "-ets" }) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) return false;
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static async Task StopSessionAsync(string sessionName)
    {
        var info = new ProcessStartInfo(AppPaths.PresentMonFile) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "--session_name", sessionName, "--stop_existing_session" }) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) return;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var wait = process.WaitForExitAsync();
        if (await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5))) != wait)
        {
            try { process.Kill(true); } catch { }
        }
        await wait;
        _ = await output;
        _ = await error;

        if (!await SessionExistsAsync(sessionName)) return;

        var fallback = new ProcessStartInfo("logman.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in BuildLogmanStopArguments(sessionName)) fallback.ArgumentList.Add(argument);
        using var fallbackProcess = new Process { StartInfo = fallback };
        if (!fallbackProcess.Start()) return;
        var fallbackOutput = fallbackProcess.StandardOutput.ReadToEndAsync();
        var fallbackError = fallbackProcess.StandardError.ReadToEndAsync();
        await fallbackProcess.WaitForExitAsync();
        _ = await fallbackOutput;
        _ = await fallbackError;

        for (var attempt = 0; attempt < 10 && await SessionExistsAsync(sessionName); attempt++)
            await Task.Delay(100);
    }

    internal static IReadOnlyList<string> BuildLogmanStopArguments(string sessionName) => ["stop", sessionName, "-ets"];

    private static void VerifyDependency()
    {
        if (!File.Exists(AppPaths.PresentMonFile) || !File.Exists(AppPaths.PresentMonManifestFile)) throw new FileNotFoundException("The bundled PresentMon dependency is missing.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(AppPaths.PresentMonManifestFile)); var expected = manifest.RootElement.GetProperty("sha256").GetString(); using var stream = File.OpenRead(AppPaths.PresentMonFile); var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The bundled PresentMon checksum is invalid.");
    }
}

public static class PresentMonCsvParser
{
    public static PerformanceMetrics Parse(string path)
    {
        using var parser = new TextFieldParser(path) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true }; parser.SetDelimiters(DetectDelimiter(File.ReadLines(path).First()));
        var headers = parser.ReadFields() ?? throw new InvalidDataException("PresentMon CSV has no header.");
        var index = FindHeader(headers, "MsBetweenPresents", "FrameTime", "CPUFrameTime", "MsBetweenDisplayChange"); if (index < 0) index = Array.FindIndex(headers, h => h.Contains("frametime", StringComparison.OrdinalIgnoreCase)); if (index < 0) throw new InvalidDataException("PresentMon CSV has no supported frametime column.");
        var values = new List<double>(); while (!parser.EndOfData) { var row = parser.ReadFields(); if (row is null || row.Length <= index) continue; if (double.TryParse(row[index].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 && value < 10000) values.Add(value); }
        if (values.Count < 120) throw new InvalidDataException("PresentMon capture contains too few frame samples.");
        values.Sort(); var total = values.Sum(); var oneCount = Math.Max(1, (int)Math.Ceiling(values.Count * 0.01)); var pointOneCount = Math.Max(1, (int)Math.Ceiling(values.Count * 0.001)); var oneMs = values.TakeLast(oneCount).Average(); var pointOneMs = values.TakeLast(pointOneCount).Average();
        return new(values.Count, Math.Round(total / 1000, 3), Math.Round(values.Count / (total / 1000), 2), Math.Round(1000 / oneMs, 2), Math.Round(1000 / pointOneMs, 2), Math.Round(total / values.Count, 3), Percentile(values, .95), Percentile(values, .99));
    }
    private static int FindHeader(string[] headers, params string[] names) { foreach (var name in names) { var index = Array.FindIndex(headers, x => x.Equals(name, StringComparison.OrdinalIgnoreCase)); if (index >= 0) return index; } return -1; }
    private static string DetectDelimiter(string header) => new[] { ",", ";", "\t" }.OrderByDescending(x => header.Split(x).Length).First();
    private static double Percentile(List<double> sorted, double percentile) => Math.Round(sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Count) - 1, 0, sorted.Count - 1)], 3);
}

public sealed class BenchmarkStore
{
    private readonly string _path;

    public BenchmarkStore(string? path = null) => _path = path ?? AppPaths.BenchmarkFile;

    public BenchmarkDocument Load()
    {
        if (!File.Exists(_path)) return new();
        using var parsed = JsonDocument.Parse(File.ReadAllText(_path)); if (!parsed.RootElement.TryGetProperty("schema_version", out var schema) || schema.GetInt32() != 1) throw new InvalidDataException("The existing benchmark.json uses an unsupported prototype schema.");
        return JsonSerializer.Deserialize<BenchmarkDocument>(parsed.RootElement.GetRawText(), JsonDefaults.Options) ?? new();
    }
    public void Append(BenchmarkRun run) { var document = Load(); document.Runs.Add(run); Save(document); }
    public void MarkSubmitted(IEnumerable<string> runIds)
    {
        var ids = runIds.ToHashSet(StringComparer.Ordinal);
        var document = Load();
        for (var index = 0; index < document.Runs.Count; index++)
        {
            var run = document.Runs[index];
            if (ids.Contains(run.RunId)) document.Runs[index] = run with { Submitted = true };
        }
        Save(document);
    }
    private void Save(BenchmarkDocument document) => AppPaths.AtomicWrite(_path, JsonSerializer.Serialize(document, JsonDefaults.Options));
}

public static class BenchmarkSubmission
{
    public const int MaxRuns = 20;
    public static IReadOnlyList<BenchmarkRun> SelectUnsubmitted(IReadOnlyList<BenchmarkRun> runs) =>
        runs.Where(run => !run.Submitted).TakeLast(MaxRuns).ToList();
    public static IReadOnlyList<BenchmarkRun> SelectMostRecent(IReadOnlyList<BenchmarkRun> runs) =>
        runs.TakeLast(MaxRuns).ToList();
    public static string SerializeLatest(IReadOnlyList<BenchmarkRun> runs)
    {
        if (runs.Count == 0) throw new InvalidOperationException("There is no completed benchmark result to copy.");
        return Serialize([runs[^1]]);
    }
    public static string Serialize(IReadOnlyList<BenchmarkRun> runs) => JsonSerializer.Serialize(new BenchmarkDocument { Runs = runs.ToList() }, JsonDefaults.Options);
}
