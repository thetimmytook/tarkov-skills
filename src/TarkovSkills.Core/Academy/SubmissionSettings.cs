using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core.Academy;

internal static class SubmissionSettings
{
    private static readonly (string Name, string Rule)[] Game = [("AutoEmptyWorkingSet", "bool"), ("SetAffinityToLogicalCores", "bool")];
    private static readonly (string Name, string Rule)[] Display = [("FullScreenMode", "screen")];
    private static readonly (string Name, string Rule)[] Dimensions = [("Width", "dimension"), ("Height", "dimension")];
    private static readonly (string Name, string Rule)[] Postfx = [("EnablePostFx", "bool")];
    private static readonly (string Name, string Rule)[] Graphics = [
        ("TextureQuality", "enum"), ("ShadowsQuality", "enum"), ("LodBias", "0:10"),
        ("OverallVisibility", "0:10000"), ("CloudsQuality", "token"), ("AntiAliasing", "token"),
        ("VolumetricLight", "token"), ("DLSSMode", "token"), ("DLSSPreset", "token"),
        ("FSR2Mode", "token"), ("FSR3Mode", "token"), ("SuperSampling", "token"),
        ("SuperSamplingFactor", "0.1:8"), ("Ssao", "token"), ("SSR", "token"),
        ("AnisotropicFiltering", "token"), ("NVidiaReflex", "token"), ("Sharpen", "0:10"),
        ("VSync", "bool"), ("DisableGameFramerateLimit", "bool"), ("LobbyFramerate", "0:1000"),
        ("GameFramerate", "0:1000"), ("HighQualityColor", "bool"), ("ZBlur", "bool"),
        ("AreaLightsInstancing", "bool"), ("ChromaticAberrations", "bool"), ("Noise", "bool"),
        ("GrassShadow", "bool"), ("SdTarkovStreets", "bool")];

    internal static JsonObject? Project(JsonNode? saved)
    {
        var result = new JsonObject { ["schema_version"] = 1 };
        var game = Section(saved?["game"], Game);
        var graphics = Section(saved?["graphics"], Graphics);
        var display = Section(saved?["graphics"]?["DisplaySettings"], Display);
        var resolution = saved?["graphics"]?["DisplaySettings"]?["Resolution"];
        if (resolution is not null)
        {
            var pair = Section(resolution, Dimensions);
            if (pair.Count != 2) throw new InvalidDataException("Saved game resolution is incomplete.");
            display["Resolution"] = pair;
        }
        if (display.Count > 0) graphics["DisplaySettings"] = display;
        var postfx = Section(saved?["postfx"], Postfx);
        if (game.Count > 0) result["game"] = game;
        if (graphics.Count > 0) result["graphics"] = graphics;
        if (postfx.Count > 0) result["postfx"] = postfx;
        return result.Count > 1 ? result : null;
    }

    internal static void ValidateStored(JsonElement saved)
    {
        if (saved.ValueKind == JsonValueKind.Null) return;
        ResourceMetricContract.Require(saved.ValueKind == JsonValueKind.Object && saved.EnumerateObject().Count() > 1 &&
            saved.TryGetProperty("schema_version", out var schema) && schema.TryGetInt32(out var version) && version == 1);
        foreach (var property in saved.EnumerateObject())
        {
            switch (property.Name)
            {
                case "schema_version": break;
                case "game": ValidateSection(property.Value, Game); break;
                case "postfx": ValidateSection(property.Value, Postfx); break;
                case "graphics":
                    ValidateSection(property.Value, Graphics, "DisplaySettings");
                    if (property.Value.TryGetProperty("DisplaySettings", out var display))
                    {
                        ValidateSection(display, Display, "Resolution");
                        if (display.TryGetProperty("Resolution", out var resolution))
                        {
                            ResourceMetricContract.Object(resolution, "Width", "Height");
                            ValidateSection(resolution, Dimensions);
                        }
                    }
                    break;
                default: ResourceMetricContract.Require(false); break;
            }
        }
    }

    internal static SubmissionResolution? Resolution(JsonElement saved) =>
        saved.ValueKind == JsonValueKind.Object && saved.TryGetProperty("graphics", out var graphics) &&
        graphics.TryGetProperty("DisplaySettings", out var display) && display.TryGetProperty("Resolution", out var resolution)
            ? new(resolution.GetProperty("Width").GetDouble(), resolution.GetProperty("Height").GetDouble()) : null;

    private static void ValidateSection(JsonElement section, (string Name, string Rule)[] fields, string? nested = null)
    {
        ResourceMetricContract.Require(section.ValueKind == JsonValueKind.Object && section.EnumerateObject().Any());
        foreach (var property in section.EnumerateObject())
        {
            if (property.Name == nested) continue;
            var rule = fields.FirstOrDefault(field => field.Name == property.Name).Rule;
            ResourceMetricContract.Require(rule is not null && Valid(JsonValue.Create(property.Value)!, rule));
        }
    }

    private static JsonObject Section(JsonNode? source, params (string Name, string Rule)[] fields)
    {
        var result = new JsonObject();
        if (source is null) return result;
        foreach (var (name, rule) in fields)
        {
            if (source[name] is not { } value) continue;
            if (value is not JsonValue scalar || !Valid(scalar, rule))
                throw new InvalidDataException("A saved setting is not supported by the submission contract.");
            result[name] = value.DeepClone();
        }
        return result;
    }

    private static bool Valid(JsonValue scalar, string rule)
    {
        if (rule == "bool") return scalar.TryGetValue<bool>(out _);
        if (rule == "token") return scalar.TryGetValue<string>(out var token) &&
            token is not null && Regex.IsMatch(token, "^[A-Za-z][A-Za-z0-9_]{0,39}$");
        if (!scalar.TryGetValue<double>(out var number) || !double.IsFinite(number)) return false;
        return rule switch
        {
            "enum" => number == Math.Truncate(number) && number is >= 0 and <= 15,
            "screen" => number is 0 or 1 or 2,
            "dimension" => number == Math.Truncate(number) && number is >= 1 and <= 16384,
            "0:10" => number is >= 0 and <= 10,
            "0:10000" => number is >= 0 and <= 10000,
            "0.1:8" => number is >= 0.1 and <= 8,
            "0:1000" => number is >= 0 and <= 1000,
            _ => false
        };
    }
}
