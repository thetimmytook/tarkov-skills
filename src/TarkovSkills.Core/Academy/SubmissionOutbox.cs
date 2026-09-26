using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace TarkovSkills.Core.Academy;

public sealed class PreparedSubmission
{
    internal string Json { get; }
    public Guid ClientRunId { get; }
    public string Summary
    {
        get
        {
            var dto = JsonNode.Parse(Json)!;
            return $"{dto["captured_day"]} · {dto["map"]} · {dto["execution"]}\n" +
                $"{dto["hardware"]!["cpu_name"]} · {dto["hardware"]!["gpu_name"]} · {dto["hardware"]!["ram_gb"]} GB RAM\n" +
                $"Average {dto["metrics"]!["average_fps"]} FPS · 1% low {dto["metrics"]!["one_percent_low_fps"]} FPS";
        }
    }
    internal PreparedSubmission(string json)
    {
        Json = json;
        ClientRunId = Guid.Parse(JsonNode.Parse(json)!["client_run_id"]!.GetValue<string>());
    }
    public override string ToString() => "Prepared benchmark submission";
}

// A frozen, public-safe request, independent of benchmark.json and its legacy submitted flag.
public sealed class SubmissionOutbox(string dataDirectory, Uri apiBaseUri, string issuer, string clientId)
{
    private readonly string directory = Path.Combine(dataDirectory, "academy-submissions", Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(apiBaseUri.AbsoluteUri.TrimEnd('/') + "\n" + issuer + "\n" + clientId))));

    // A historical observation only: never evidence of the currently signed-in owner.
    public PublicationCheckpoint? ReadStatus(Guid id)
    {
        var file = Path.Combine(directory, id.ToString("D") + ".status.json");
        if (!File.Exists(file)) return null;
        if (new FileInfo(file).Length > 2048) throw new InvalidDataException("Saved status is invalid.");
        var value = JsonSerializer.Deserialize<PublicationCheckpoint>(File.ReadAllText(file));
        if (value is null || value.ClientRunId != id || !PublicationCheckpoint.IsConfirmed(value.Status) ||
            value.CheckedAt == default || value.CheckedAt > DateTimeOffset.UtcNow.AddMinutes(1))
            throw new InvalidDataException("Saved status is invalid.");
        return value;
    }

    internal PublicationCheckpoint SaveStatus(Guid id, string status)
    {
        if (!PublicationCheckpoint.IsConfirmed(status)) throw new InvalidDataException("Unknown publication status.");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, id.ToString("D") + ".status.json");
        using var lease = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var previous = ReadStatus(id);
        // An older response from another window must never erase the deletion tombstone.
        if (previous?.Status == "deleted" && status != "deleted") return previous;
        var checkpoint = new PublicationCheckpoint(id, status, DateTimeOffset.UtcNow);
        AppPaths.AtomicWrite(file, JsonSerializer.Serialize(checkpoint));
        return checkpoint;
    }

    public PreparedSubmission Prepare(BenchmarkRun run, string selectedGpu)
    {
        if (!Guid.TryParse(run.RunId, out var id)) throw new InvalidDataException("This run has no valid ID.");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, id.ToString("D") + ".json");
        using var lease = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(file))
        {
            if (new FileInfo(file).Length > 32768) throw new InvalidDataException("Saved submission is invalid.");
            var saved = File.ReadAllText(file);
            SubmissionPayload.ValidateStored(saved, id);
            return new(saved);
        }
        var json = SubmissionPayload.Create(run, selectedGpu);
        if (Encoding.UTF8.GetByteCount(json) > 32768) throw new InvalidDataException("This submission exceeds the API size limit.");
        AppPaths.AtomicWrite(file, json);
        return new(json);
    }
}
