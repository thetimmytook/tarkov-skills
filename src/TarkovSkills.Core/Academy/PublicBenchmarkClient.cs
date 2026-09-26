using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace TarkovSkills.Core.Academy;

// Anonymous, read-only endpoints. This client has no access to desktop credentials.
public sealed class PublicBenchmarkClient : IDisposable
{
    private readonly HttpClient http;
    public PublicBenchmarkClient(AcademyConfiguration config)
        : this(config, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { }

    internal PublicBenchmarkClient(AcademyConfiguration config, HttpMessageHandler handler)
    {
        var uri = config.BaseUri;
        if (!uri.IsAbsoluteUri || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.AbsolutePath.TrimEnd('/') != "/api/bench/v1" ||
            !(uri.Scheme == "https" || config.AllowLoopbackHttp && uri.Scheme == "http" && uri.Host == "127.0.0.1"))
        {
            handler.Dispose();
            throw new ArgumentException("Invalid public API configuration.");
        }
        http = new HttpClient(handler) { BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 262144 };
    }

    public async Task<ComparisonResult> BrowseAsync(string map, CancellationToken cancellation)
    {
        if (!BenchmarkComparison.Maps.ContainsKey(map)) throw new ArgumentException("Unknown map.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "runs?view=groups&limit=6&map=" + Uri.EscapeDataString(map));
        var root = await ReadAsync(request, cancellation);
        if (root["view"]?.GetValue<string>() != "groups") throw new InvalidDataException();
        var rows = new List<ComparisonBar>();
        foreach (var group in root["groups"]!.AsArray())
            foreach (var run in group!["preview_runs"]!.AsArray())
            {
                if (run!["map"]!["id"]!.GetValue<string>() != map) throw new InvalidDataException();
                rows.Add(BenchmarkComparison.ReadRun(run, BenchmarkComparison.Hardware(group["hardware"]!)));
            }
        if (rows.Count > 18) throw new InvalidDataException();
        return new(rows, $"{rows.Count} public examples shown · {root["summary"]!["run_count"]} runs on this map. " +
            "Hardware and conditions vary; these examples are not a ranking.");
    }

    public async Task<ComparisonResult> CompareAsync(BenchmarkRun local, string gpu, CancellationToken cancellation)
    {
        var query = BenchmarkComparison.Query(local, gpu);
        using var request = new HttpRequestMessage(HttpMethod.Post, "cohorts/query")
        { Content = new StringContent(query, Encoding.UTF8, "application/json") };
        var root = await ReadAsync(request, cancellation);
        var status = root["status"]!.GetValue<string>();
        if (status is not ("matches" or "no_data" or "missing_conditions")) throw new InvalidDataException();
        var rows = new List<ComparisonBar>
        {
            new("Your selected run", $"{local.CollectedDate} · {gpu} · Saved locally",
                local.Performance.AverageFps, local.Performance.OnePercentLowFps, true, false)
        };
        var hardware = BenchmarkComparison.Hardware(root["criteria"]!["hardware"]!);
        foreach (var run in root["runs"]!.AsArray()) rows.Add(BenchmarkComparison.ReadRun(run!, hardware));
        if (rows.Count > 21 || status != "matches" && rows.Count != 1) throw new InvalidDataException();
        var criteria = JsonNode.Parse(query)!;
        var explanation = status switch
        {
            "missing_conditions" => "Resolution or game version is missing. Exact comparison is unavailable.",
            "no_data" => "No public runs match these conditions yet.",
            _ => $"{rows.Count - 1} of {root["counts"]!["run_count"]} matching public runs shown · " +
                $"{root["counts"]!["contributor_count"]} contributors." +
                (root["truncated"]!.GetValue<bool>() ? " Results limited to the latest 20." : "")
        };
        return new(rows, $"{hardware} · {criteria["map"]} · {criteria["execution"]} · " +
            $"{criteria["game_resolution"]?["width"]} × {criteria["game_resolution"]?["height"]} · {criteria["game_version"]}\n" +
            explanation + " Settings and weather may differ. Demo runs are labelled; no percentile is calculated.");
    }

    private async Task<JsonNode> ReadAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        using var response = await http.SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Public benchmark request failed.");
        return JsonNode.Parse(await response.Content.ReadAsByteArrayAsync(cancellation)) ?? throw new InvalidDataException();
    }

    public void Dispose() => http.Dispose();
}
