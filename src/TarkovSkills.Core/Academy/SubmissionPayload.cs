using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TarkovSkills.Core.Academy;

public static class SubmissionPayload
{
    // Reproject a saved DTO before reuse. Unknown fields or changed schema cannot bypass
    // the privacy allowlist merely because a file already exists in local storage.
    internal static void ValidateStored(string json, Guid id)
    {
        var dto = JsonNode.Parse(json) ?? throw new InvalidDataException();
        var hardware = dto["hardware"]!;
        var conditions = dto["context"]!;
        var metrics = dto["metrics"]!;
        var map = dto["map"]!.GetValue<string>() switch
        {
            "streets" => "Streets of Tarkov", "customs" => "Customs", "lighthouse" => "Lighthouse", "woods" => "Woods",
            "factory" => "Factory", "the-lab" => "The Lab", "reserve" => "Reserve", "ground-zero" => "Ground Zero",
            "interchange" => "Interchange", "shoreline" => "Shoreline", "labyrinth" => "Labyrinth",
            _ => throw new InvalidDataException()
        };
        var run = new BenchmarkRun(id.ToString("D"), dto["captured_day"]!.GetValue<string>(), 120,
            dto["app_version"]!.GetValue<string>(),
            new { cpu = new { name = hardware["cpu_name"]!.GetValue<string>() },
                gpu = new[] { new { name = hardware["gpu_name"]!.GetValue<string>() } },
                ram = new { total_gb = hardware["ram_gb"]!.GetValue<double>() } },
            dto["settings_snapshot"]?.DeepClone() ?? new JsonObject(),
            new { map, execution = dto["execution"]!.GetValue<string>(),
                weather = conditions["weather"]!.GetValue<string>(), time_of_day = conditions["time_of_day"]!.GetValue<string>(),
                game_version = dto["game_version"]?.GetValue<string>() },
            new PerformanceMetrics(dto["capture"]!["sample_count"]!.GetValue<int>(), dto["capture"]!["duration_sec"]!.GetValue<double>(),
                metrics["average_fps"]!.GetValue<double>(), metrics["one_percent_low_fps"]!.GetValue<double>(),
                metrics["zero_point_one_percent_low_fps"]!.GetValue<double>(), metrics["average_frametime_ms"]!.GetValue<double>(),
                metrics["p95_frametime_ms"]!.GetValue<double>(), metrics["p99_frametime_ms"]!.GetValue<double>()), []);
        var projected = JsonNode.Parse(Create(run, hardware["gpu_name"]!.GetValue<string>()));
        if (!JsonNode.DeepEquals(dto, projected)) throw new InvalidDataException("Saved submission does not match the current contract.");
    }

    private static JsonNode? Node(object value) => JsonSerializer.SerializeToNode(value, JsonDefaults.Options);
    public static IReadOnlyList<string> GpuNames(BenchmarkRun run) =>
        (Node(run.System)?["gpu"] as JsonArray)?.Select(gpu => Label(gpu?["name"])).Distinct().ToArray() ?? [];

    public static string Create(BenchmarkRun run, string selectedGpu)
    {
        if (!Guid.TryParse(run.RunId, out var id) ||
            !DateOnly.TryParseExact(run.CollectedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("This run has no valid ID or capture date.");
        var system = Node(run.System);
        var context = Node(run.Context);
        var settings = SubmissionSettings.Project(Node(run.Settings));
        var map = Label(context?["map"]) switch
        {
            "Streets of Tarkov" => "streets", "Customs" => "customs",
            "Lighthouse" => "lighthouse", "Woods" => "woods",
            "Factory" => "factory", "The Lab" => "the-lab", "Reserve" => "reserve", "Ground Zero" => "ground-zero",
            "Interchange" => "interchange", "Shoreline" => "shoreline", "Labyrinth" => "labyrinth",
            _ => throw new InvalidDataException("This run has an unknown map.")
        };
        if (!GpuNames(run).Contains(selectedGpu)) throw new InvalidDataException("Select the GPU used for this run.");
        var ram = system?["ram"]?["total_gb"]?.GetValue<double>() ?? 0;
        if (!double.IsFinite(ram) || ram <= 0 || ram != Math.Truncate(ram))
            throw new InvalidDataException("This run does not contain a supported RAM capacity.");
        var resolution = settings?["graphics"]?["DisplaySettings"]?["Resolution"];
        var performance = run.Performance;
        ValidateMetrics(performance);
        var dto = new JsonObject
        {
            ["schema_version"] = 1, ["client_run_id"] = id.ToString("D"),
            ["captured_day"] = run.CollectedDate, ["app_version"] = Label(JsonValue.Create(run.AppVersion)),
            ["hardware"] = new JsonObject { ["cpu_name"] = Label(system?["cpu"]?["name"]), ["gpu_name"] = selectedGpu, ["ram_gb"] = ram },
            ["map"] = map, ["execution"] = Choice(context?["execution"], "bsg_servers", "local"),
            ["game_resolution"] = resolution is null ? null : new JsonObject { ["width"] = resolution["Width"]!.DeepClone(), ["height"] = resolution["Height"]!.DeepClone() },
            ["game_version"] = context?["game_version"] is null ? null : Label(context["game_version"]),
            ["context"] = new JsonObject {
                ["weather"] = Choice(context?["weather"], "unknown", "clear", "cloudy", "rain", "fog", "snow"),
                ["time_of_day"] = Choice(context?["time_of_day"], "unknown", "day", "night", "dawn_dusk") },
            ["settings_snapshot"] = settings,
            ["capture"] = new JsonObject { ["duration_sec"] = performance.DurationSec, ["sample_count"] = performance.SampleCount },
            ["metrics"] = new JsonObject {
                ["average_fps"] = performance.AverageFps, ["one_percent_low_fps"] = performance.OnePercentLowFps,
                ["zero_point_one_percent_low_fps"] = performance.ZeroPointOnePercentLowFps,
                ["average_frametime_ms"] = performance.AverageFrametimeMs,
                ["p95_frametime_ms"] = performance.P95FrametimeMs, ["p99_frametime_ms"] = performance.P99FrametimeMs }
        };
        return dto.ToJsonString();
    }

    private static string Label(JsonNode? node)
    {
        var text = node?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 160 || text.Any(char.IsControl) || text.Contains('\\') || text.Contains('/'))
            throw new InvalidDataException("Required run information is missing or invalid.");
        return text;
    }
    private static string Choice(JsonNode? node, params string[] values)
    {
        var value = Label(node);
        if (!values.Contains(value)) throw new InvalidDataException("Saved run conditions are not supported.");
        return value;
    }
    private static void ValidateMetrics(PerformanceMetrics p)
    {
        double[] values = [p.DurationSec, p.AverageFps, p.OnePercentLowFps, p.ZeroPointOnePercentLowFps, p.AverageFrametimeMs, p.P95FrametimeMs, p.P99FrametimeMs];
        if (values.Any(v => !double.IsFinite(v) || v < 0) || p.DurationSec < 110 || p.SampleCount < 120 ||
            p.AverageFps <= 0 || p.AverageFrametimeMs <= 0 || p.P95FrametimeMs <= 0 ||
            p.OnePercentLowFps > p.AverageFps || p.ZeroPointOnePercentLowFps > p.OnePercentLowFps || p.P95FrametimeMs > p.P99FrametimeMs)
            throw new InvalidDataException("This run does not meet Academy capture requirements.");
        var earliest = Math.Max(p.DurationSec - .0005, Math.Max(p.SampleCount * (p.AverageFrametimeMs - .005) / 1000, p.SampleCount / (p.AverageFps + .005)));
        var latest = Math.Min(p.DurationSec + .0005, Math.Min(p.SampleCount * (p.AverageFrametimeMs + .005) / 1000,
            p.AverageFps > .005 ? p.SampleCount / (p.AverageFps - .005) : double.PositiveInfinity));
        if (earliest > latest + 1e-9) throw new InvalidDataException("Saved capture metrics are inconsistent.");
    }
}
