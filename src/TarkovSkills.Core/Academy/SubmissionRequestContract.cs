using System.Globalization;
using System.Text.Json;

namespace TarkovSkills.Core.Academy;

internal static class SubmissionRequestContract
{
    internal static readonly IReadOnlyDictionary<string, string> Maps = new Dictionary<string, string>
    {
        ["Streets of Tarkov"] = "streets", ["Customs"] = "customs", ["Lighthouse"] = "lighthouse", ["Woods"] = "woods",
        ["Factory"] = "factory", ["The Lab"] = "the-lab", ["Reserve"] = "reserve", ["Ground Zero"] = "ground-zero",
        ["Interchange"] = "interchange", ["Shoreline"] = "shoreline", ["Labyrinth"] = "labyrinth"
    };

    internal static void Validate(string json, Guid id)
    {
        using var document = JsonDocument.Parse(json);
        RejectDuplicateKeys(document.RootElement);
        Require(document.RootElement.ValueKind == JsonValueKind.Object);
        if (!document.RootElement.TryGetProperty("resource_telemetry", out _))
            throw new InvalidDataException(SubmissionPayload.UnsupportedSavedPayload);
        var request = document.RootElement.Deserialize<SubmissionRequest>(JsonDefaults.Options)!;
        Require(request is not null && request.SchemaVersion == 1 && request.ClientRunId == id.ToString("D"));
        Require(DateOnly.TryParseExact(request!.CapturedDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
        Label(request.AppVersion);
        Require(request.Hardware is not null && request.Context is not null && request.Capture is not null && request.Metrics is not null);
        Label(request.Hardware!.CpuName); Label(request.Hardware.GpuName);
        Require(double.IsFinite(request.Hardware.RamGb) && request.Hardware.RamGb > 0 && request.Hardware.RamGb == Math.Truncate(request.Hardware.RamGb));
        Require(Maps.Values.Contains(request.Map));
        Choice(request.Execution, "bsg_servers", "local");
        Choice(request.Context!.Weather, "unknown", "clear", "cloudy", "rain", "fog", "snow");
        Choice(request.Context.TimeOfDay, "unknown", "day", "night", "dawn_dusk");
        if (request.GameVersion is not null) Label(request.GameVersion);
        SubmissionSettings.ValidateStored(request.SettingsSnapshot);
        Require(request.GameResolution == SubmissionSettings.Resolution(request.SettingsSnapshot));
        ValidateMetrics(request.Capture!, request.Metrics!);
        ResourceTelemetryContract.Read(request.ResourceTelemetry, request.Capture!.DurationSec, request.Capture.SampleCount);
    }

    internal static void Label(string? text) => Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 160 &&
        !text.Any(char.IsControl) && !text.Contains('\\') && !text.Contains('/'));
    private static void Choice(string? value, params string[] choices) => Require(choices.Contains(value));
    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Saved submission does not match the supported Academy contract. Nothing was sent.");
    }

    private static void RejectDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            Require(properties.Select(p => p.Name).Distinct().Count() == properties.Length);
            foreach (var property in properties) RejectDuplicateKeys(property.Value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateKeys(item);
    }

    internal static void ValidateMetrics(SubmissionCapture capture, SubmissionMetrics p)
    {
        double[] values = [capture.DurationSec, p.AverageFps, p.OnePercentLowFps, p.ZeroPointOnePercentLowFps,
            p.AverageFrametimeMs, p.P95FrametimeMs, p.P99FrametimeMs];
        Require(!values.Any(v => !double.IsFinite(v) || v < 0) && capture.DurationSec >= 110 && capture.SampleCount >= 120 &&
            p.AverageFps > 0 && p.AverageFrametimeMs > 0 && p.P95FrametimeMs > 0 &&
            p.OnePercentLowFps <= p.AverageFps && p.ZeroPointOnePercentLowFps <= p.OnePercentLowFps && p.P95FrametimeMs <= p.P99FrametimeMs);
        var earliest = Math.Max(capture.DurationSec - .0005, Math.Max(capture.SampleCount * (p.AverageFrametimeMs - .005) / 1000,
            capture.SampleCount / (p.AverageFps + .005)));
        var latest = Math.Min(capture.DurationSec + .0005, Math.Min(capture.SampleCount * (p.AverageFrametimeMs + .005) / 1000,
            p.AverageFps > .005 ? capture.SampleCount / (p.AverageFps - .005) : double.PositiveInfinity));
        Require(earliest <= latest + 1e-9);
    }
}
