using System.Text.Json;
using System.Text.Json.Serialization;

namespace TarkovSkills.Core.Academy;

// The frozen API request is independent of the local history/storage model.
// Nullable properties are required keys, and every nested object rejects extras.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionRequest(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string ClientRunId,
    [property: JsonRequired] string CapturedDay,
    [property: JsonRequired] string AppVersion,
    [property: JsonRequired] SubmissionHardware Hardware,
    [property: JsonRequired] string Map,
    [property: JsonRequired] string Execution,
    [property: JsonRequired] SubmissionResolution? GameResolution,
    [property: JsonRequired] string? GameVersion,
    [property: JsonRequired] SubmissionConditions Context,
    [property: JsonRequired] JsonElement SettingsSnapshot,
    [property: JsonRequired] SubmissionCapture Capture,
    [property: JsonRequired] SubmissionMetrics Metrics,
    [property: JsonRequired] JsonElement ResourceTelemetry);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionHardware(
    [property: JsonRequired] string CpuName,
    [property: JsonRequired] string GpuName,
    [property: JsonRequired] double RamGb);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionResolution(
    [property: JsonRequired] double Width, [property: JsonRequired] double Height);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionConditions(
    [property: JsonRequired] string Weather, [property: JsonRequired] string TimeOfDay);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionCapture(
    [property: JsonRequired] double DurationSec, [property: JsonRequired] int SampleCount);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmissionMetrics(
    [property: JsonRequired] double AverageFps,
    [property: JsonRequired] double OnePercentLowFps,
    [property: JsonRequired] double ZeroPointOnePercentLowFps,
    [property: JsonRequired] double AverageFrametimeMs,
    [property: JsonRequired] double P95FrametimeMs,
    [property: JsonRequired] double P99FrametimeMs);
