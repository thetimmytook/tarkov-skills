using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TarkovSkills.Core.Academy;

public static class SubmissionPayload
{
    internal const int MaxPayloadBytes = 262144;
    internal const string UnsupportedSavedPayload = "This saved submission uses an unsupported pre-telemetry contract. It was not changed or sent. Collect a new run and review it before sending.";
    // Validate the frozen request directly. Unknown fields or changed schema cannot bypass
    // the privacy allowlist merely because a file already exists in local storage.
    internal static void ValidateStored(string json, Guid id)
    {
        try { SubmissionRequestContract.Validate(json, id); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or NullReferenceException)
        { throw new InvalidDataException("Saved submission does not match the supported Academy contract. Nothing was sent."); }
    }

    private static JsonNode? Node(object value) => JsonSerializer.SerializeToNode(value, JsonDefaults.Options);
    private static JsonNode Resource(ResourceTelemetry telemetry, PerformanceMetrics performance)
    {
        try
        {
            // ResourceTelemetry contains summaries only. Validate all fixed scopes/sources,
            // labels and metadata before this typed allowlist crosses the upload boundary.
            var element = JsonSerializer.SerializeToElement(telemetry, JsonDefaults.Options);
            ResourceTelemetryContract.Read(element, performance.DurationSec, performance.SampleCount);
            return JsonNode.Parse(element.GetRawText())!;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        { throw new InvalidDataException("Capture-resource summary does not match the supported Academy contract. Nothing was sent."); }
    }
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
        if (!SubmissionRequestContract.Maps.TryGetValue(Label(context?["map"]), out var map))
            throw new InvalidDataException("This run has an unknown map.");
        if (!GpuNames(run).Contains(selectedGpu)) throw new InvalidDataException("Select the GPU used for this run.");
        var ram = system?["ram"]?["total_gb"]?.GetValue<double>() ?? 0;
        if (!double.IsFinite(ram) || ram <= 0 || ram != Math.Truncate(ram))
            throw new InvalidDataException("This run does not contain a supported RAM capacity.");
        var resolution = settings?["graphics"]?["DisplaySettings"]?["Resolution"];
        var performance = run.Performance;
        SubmissionRequestContract.ValidateMetrics(new(performance.DurationSec, performance.SampleCount),
            new(performance.AverageFps, performance.OnePercentLowFps, performance.ZeroPointOnePercentLowFps,
                performance.AverageFrametimeMs, performance.P95FrametimeMs, performance.P99FrametimeMs));
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
                ["p95_frametime_ms"] = performance.P95FrametimeMs, ["p99_frametime_ms"] = performance.P99FrametimeMs },
            ["resource_telemetry"] = Resource(run.ResourceTelemetry, performance)
        };
        return dto.ToJsonString();
    }

    private static string Label(JsonNode? node)
    {
        var text = node?.GetValue<string>();
        SubmissionRequestContract.Label(text);
        return text!;
    }
    private static string Choice(JsonNode? node, params string[] values)
    {
        var value = Label(node);
        if (!values.Contains(value)) throw new InvalidDataException("Saved run conditions are not supported.");
        return value;
    }
}
