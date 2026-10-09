using System.Text.Json;

namespace TarkovSkills.Core.Academy;

// The desktop boundary mirrors Academy's strict resource-metric v1 contract.
// No Windows errors or untrusted field values enter the validation message.
internal static class ResourceMetricContract
{
    internal const double DurationTolerance = .0000005, CoverageTolerance = .0000005,
        CompleteTolerance = .000001, FloatingSlack = .000000001, MaxSafeInteger = 9007199254740991;
    private static readonly HashSet<string> Reasons = ["not_collected", "counter_unavailable", "window_unavailable",
        "summary_unavailable", "collector_unavailable", "active_adapter_unknown", "multiple_active_adapters",
        "linked_adapter_unsupported", "memory_architecture_unknown", "partial_coverage", "pagefile_management_unknown"];

    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Capture-resource summary does not match the supported Academy contract. Nothing was sent.");
    }

    internal static void Object(JsonElement node, params string[] keys)
    {
        Require(node.ValueKind == JsonValueKind.Object);
        var names = node.EnumerateObject().Select(p => p.Name).ToArray();
        Require(names.Length == keys.Length && names.Distinct().Count() == names.Length && names.All(keys.Contains));
    }

    internal static string Choice(JsonElement node, params string[] choices)
    {
        Require(node.ValueKind == JsonValueKind.String);
        var value = node.GetString()!;
        Require(choices.Contains(value));
        return value;
    }

    internal static double Number(JsonElement node, double maximum = MaxSafeInteger, bool integer = false)
    {
        Require(node.ValueKind == JsonValueKind.Number && node.TryGetDouble(out _));
        var number = node.GetDouble();
        Require(double.IsFinite(number) && number >= 0 && number <= maximum && (!integer || number == Math.Truncate(number)));
        return number;
    }

    internal static double? NullableNumber(JsonElement node, double maximum = MaxSafeInteger, bool integer = false) =>
        node.ValueKind == JsonValueKind.Null ? null : Number(node, maximum, integer);

    internal static void ReasonCodes(JsonElement node)
    {
        Require(node.ValueKind == JsonValueKind.Array && node.GetArrayLength() <= 16);
        var values = node.EnumerateArray().Select(n => Choice(n, Reasons.ToArray())).ToArray();
        Require(values.Distinct().Count() == values.Length);
    }

    internal static void Metric(JsonElement node, string unit, string source, string scope, params string[] additionalSources)
    {
        Object(node, "average", "minimum", "maximum", "last", "valid_sample_count", "valid_duration_sec", "coverage",
            "unit", "source", "scope", "status", "reason_codes");
        Choice(node.GetProperty("unit"), unit);
        var actualSource = Choice(node.GetProperty("source"), [source, .. additionalSources]);
        Choice(node.GetProperty("scope"), scope);
        var maximumValue = unit == "percent" ? 100 : MaxSafeInteger;
        var average = NullableNumber(node.GetProperty("average"), maximumValue);
        var minimum = NullableNumber(node.GetProperty("minimum"), maximumValue, unit == "count");
        var maximum = NullableNumber(node.GetProperty("maximum"), maximumValue, unit == "count");
        var last = NullableNumber(node.GetProperty("last"), maximumValue, unit == "count");
        var count = Number(node.GetProperty("valid_sample_count"), 10000, true);
        var duration = Number(node.GetProperty("valid_duration_sec"), double.MaxValue);
        var coverage = Number(node.GetProperty("coverage"), 1);
        var status = Choice(node.GetProperty("status"), "available", "partial", "unavailable");
        var reasons = node.GetProperty("reason_codes"); ReasonCodes(reasons);
        var codes = reasons.EnumerateArray().Select(r => r.GetString()).ToArray();
        if (status == "unavailable")
        {
            Require(average is null && minimum is null && maximum is null && last is null && count == 0 && duration == 0 && coverage == 0 &&
                codes.Length > 0 && !codes.Contains("partial_coverage"));
            return;
        }
        Require(average.HasValue && minimum.HasValue && maximum.HasValue && last.HasValue && count > 0);
        // Number.EPSILON in JS is the distance between 1 and its next representable value.
        var slack = 2.220446049250313e-16 * Math.Max(1, maximum!.Value) * 8;
        Require(minimum <= maximum && average >= minimum - slack && average <= maximum + slack && last >= minimum && last <= maximum);
        var sampleSeconds = actualSource.StartsWith("pdh_", StringComparison.Ordinal) && unit == "percent" ? 1.5 : 1;
        Require(duration <= count * sampleSeconds + DurationTolerance);
        Require(status == "available"
            ? coverage >= 1 - (CompleteTolerance + CoverageTolerance) && codes.Length == 0
            : coverage < 1 - (CompleteTolerance - CoverageTolerance) && codes.SequenceEqual(new[] { "partial_coverage" }));
    }

    internal static void Capacity(JsonElement node, string scope, params string[] sources)
    {
        Object(node, "value", "unit", "source", "scope", "status", "reason_codes");
        var value = NullableNumber(node.GetProperty("value"), integer: true);
        Choice(node.GetProperty("unit"), "bytes"); Choice(node.GetProperty("source"), sources); Choice(node.GetProperty("scope"), scope);
        var status = Choice(node.GetProperty("status"), "available", "unavailable");
        var reasons = node.GetProperty("reason_codes"); ReasonCodes(reasons);
        Require(status == "available" ? value.HasValue && reasons.GetArrayLength() == 0 : value is null &&
            reasons.GetArrayLength() > 0 && !reasons.EnumerateArray().Any(r => r.GetString() == "partial_coverage"));
    }
}
