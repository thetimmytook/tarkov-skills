using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core.Academy;

internal static class SubmissionSettings
{
    internal static JsonObject? Project(JsonNode? saved)
    {
        var result = new JsonObject { ["schema_version"] = 1 };
        var game = Section(saved?["game"], ("AutoEmptyWorkingSet", "bool"), ("SetAffinityToLogicalCores", "bool"));
        var graphics = Section(saved?["graphics"],
            ("TextureQuality", "enum"), ("ShadowsQuality", "enum"), ("LodBias", "0:10"),
            ("OverallVisibility", "0:10000"), ("CloudsQuality", "token"), ("AntiAliasing", "token"),
            ("VolumetricLight", "token"), ("DLSSMode", "token"), ("DLSSPreset", "token"),
            ("FSR2Mode", "token"), ("FSR3Mode", "token"), ("SuperSampling", "token"),
            ("SuperSamplingFactor", "0.1:8"), ("Ssao", "token"), ("SSR", "token"),
            ("AnisotropicFiltering", "token"), ("NVidiaReflex", "token"), ("Sharpen", "0:10"),
            ("VSync", "bool"), ("DisableGameFramerateLimit", "bool"), ("LobbyFramerate", "0:1000"),
            ("GameFramerate", "0:1000"), ("HighQualityColor", "bool"), ("ZBlur", "bool"),
            ("AreaLightsInstancing", "bool"), ("ChromaticAberrations", "bool"), ("Noise", "bool"),
            ("GrassShadow", "bool"), ("SdTarkovStreets", "bool"));
        var display = Section(saved?["graphics"]?["DisplaySettings"], ("FullScreenMode", "screen"));
        var resolution = saved?["graphics"]?["DisplaySettings"]?["Resolution"];
        if (resolution is not null)
        {
            var pair = Section(resolution, ("Width", "dimension"), ("Height", "dimension"));
            if (pair.Count != 2) throw new InvalidDataException("Saved game resolution is incomplete.");
            display["Resolution"] = pair;
        }
        if (display.Count > 0) graphics["DisplaySettings"] = display;
        var postfx = Section(saved?["postfx"], ("EnablePostFx", "bool"));
        if (game.Count > 0) result["game"] = game;
        if (graphics.Count > 0) result["graphics"] = graphics;
        if (postfx.Count > 0) result["postfx"] = postfx;
        return result.Count > 1 ? result : null;
    }

    private static JsonObject Section(JsonNode? source, params (string Name, string Rule)[] fields)
    {
        var result = new JsonObject();
        if (source is null) return result;
        foreach (var (name, rule) in fields)
        {
            if (source[name] is not { } value) continue;
            var valid = false;
            if (value is JsonValue scalar)
            {
                if (rule == "bool") valid = scalar.TryGetValue<bool>(out _);
                else if (rule == "token") valid = scalar.TryGetValue<string>(out var token) &&
                    Regex.IsMatch(token, "^[A-Za-z][A-Za-z0-9_]{0,39}$");
                else if (scalar.TryGetValue<double>(out var number) && double.IsFinite(number))
                {
                    valid = rule switch
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
            if (!valid) throw new InvalidDataException("A saved setting is not supported by the submission contract.");
            result[name] = value.DeepClone();
        }
        return result;
    }
}
