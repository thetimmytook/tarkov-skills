using System.Text.Json.Nodes;

namespace TarkovSkills.Core.Academy;

public sealed record ComparisonBar(string Label, string Conditions, double AverageFps, double OnePercentLowFps,
    bool IsLocal, bool IsSynthetic);
public sealed record ComparisonResult(IReadOnlyList<ComparisonBar> Runs, string Description);

public static class BenchmarkComparison
{
    public static IReadOnlyDictionary<string, string> Maps { get; } = new Dictionary<string, string>
    {
        ["streets"] = "Streets of Tarkov", ["customs"] = "Customs", ["factory"] = "Factory",
        ["the-lab"] = "The Lab", ["lighthouse"] = "Lighthouse", ["reserve"] = "Reserve",
        ["ground-zero"] = "Ground Zero", ["interchange"] = "Interchange", ["shoreline"] = "Shoreline",
        ["woods"] = "Woods", ["labyrinth"] = "Labyrinth"
    };

    // Reuse the existing privacy boundary, then send only the cohort query fields.
    // Capture metrics, settings and local run ID never leave through this endpoint.
    public static string Query(BenchmarkRun run, string gpu)
    {
        var submission = JsonNode.Parse(SubmissionPayload.Create(run, gpu))!;
        var query = new JsonObject();
        foreach (var key in new[] { "hardware", "map", "execution", "game_resolution", "game_version" })
            query[key] = submission[key]?.DeepClone();
        return query.ToJsonString();
    }

    internal static ComparisonBar ReadRun(JsonNode run, string hardware)
    {
        var metrics = run["metrics"]!;
        var avg = metrics["average_fps"]!.GetValue<double>();
        var low = metrics["one_percent_low_fps"]!.GetValue<double>();
        if (!double.IsFinite(avg) || !double.IsFinite(low) || avg <= 0 || low < 0 || low > avg)
            throw new InvalidDataException("Invalid public metrics.");
        var resolution = run["game_resolution"];
        var mode = run["execution"]!.GetValue<string>() switch
        { "local" => "Local", "bsg_servers" => "BSG servers", _ => throw new InvalidDataException() };
        var conditions = $"{Text(run["captured_day"])} · {Text(run["map"]!["name"])} · {mode} · " +
            (resolution is null ? "Resolution unknown" : $"{resolution["width"]} × {resolution["height"]}") +
            $" · {Text(run["game_version"], "Version unknown")}";
        return new(hardware, conditions, avg, low, false, run["is_synthetic"]!.GetValue<bool>());
    }

    internal static string Hardware(JsonNode value) =>
        $"{Text(value["cpu"]!["name"])} / {Text(value["gpu"]!["name"])} · {value["ram_gb"]} GB";

    private static string Text(JsonNode? value, string fallback = "Unknown")
    {
        if (value is null) return fallback;
        var text = value.GetValue<string>();
        if (text.Length > 160 || text.Any(char.IsControl)) throw new InvalidDataException();
        return text;
    }
}
