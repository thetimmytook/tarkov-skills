using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class FrozenRequestValidationTests
{
    [Theory]
    [InlineData("schema_version", "2")]
    [InlineData("client_run_id", "\"871e06df-5c0f-447a-a3b7-723492a2e09f\"")]
    [InlineData("captured_day", "\"2026-02-30\"")]
    [InlineData("app_version", "\"C:/private/app\"")]
    [InlineData("map", "\"private-map\"")]
    [InlineData("execution", "\"private\"")]
    [InlineData("hardware", "null")]
    [InlineData("context", "null")]
    [InlineData("capture", "null")]
    [InlineData("metrics", "null")]
    [InlineData("hardware.private", "\"private@example.com\"")]
    [InlineData("hardware.ram_gb", "31.5")]
    [InlineData("hardware.gpu_name", "null")]
    [InlineData("context.private", "\"private@example.com\"")]
    [InlineData("context.weather", "\"private-weather\"")]
    [InlineData("capture.private", "42")]
    [InlineData("metrics.private", "42")]
    [InlineData("game_resolution.private", "42")]
    [InlineData("game_resolution.width", "1920")]
    [InlineData("metrics.average_fps", "99")]
    [InlineData("metrics.one_percent_low_fps", "51")]
    [InlineData("metrics.p99_frametime_ms", "1")]
    [InlineData("settings_snapshot.private", "\"private@example.com\"")]
    [InlineData("settings_snapshot.schema_version", "2")]
    [InlineData("settings_snapshot.game", "{}")]
    [InlineData("settings_snapshot.postfx", "null")]
    [InlineData("settings_snapshot.graphics.private", "\"C:/private/file\"")]
    [InlineData("settings_snapshot.graphics.TextureQuality", "true")]
    [InlineData("settings_snapshot.graphics.VSync", "\"false\"")]
    [InlineData("settings_snapshot.graphics.DLSSMode", "\"private@example.com\"")]
    [InlineData("settings_snapshot.graphics.DisplaySettings.private", "42")]
    [InlineData("settings_snapshot.graphics.DisplaySettings.FullScreenMode", "3")]
    [InlineData("settings_snapshot.graphics.DisplaySettings.Resolution.private", "42")]
    [InlineData("settings_snapshot.graphics.DisplaySettings.Resolution.Width", "0")]
    public void TypedValidationStillRejectsUnknownPrivateAndInconsistentFrozenFields(string path, string value)
    {
        var run = SubmissionTests.Run();
        var json = JsonNode.Parse(SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070"))!;
        var keys = path.Split('.');
        var parent = json;
        foreach (var key in keys[..^1]) parent = parent[key]!;
        parent[keys[^1]] = JsonNode.Parse(value);
        var error = Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json.ToJsonString(), Guid.Parse(run.RunId)));
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("game_version")]
    [InlineData("game_resolution")]
    [InlineData("settings_snapshot")]
    [InlineData("hardware.cpu_name")]
    [InlineData("context.weather")]
    [InlineData("capture.duration_sec")]
    [InlineData("metrics.average_fps")]
    [InlineData("settings_snapshot.schema_version")]
    [InlineData("settings_snapshot.graphics.DisplaySettings.Resolution.Width")]
    public void RequiredKeysCannotDisappearEvenWhenTheirValuesMayBeNull(string path)
    {
        var run = SubmissionTests.Run();
        var json = JsonNode.Parse(SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070"))!;
        var keys = path.Split('.');
        var parent = json;
        foreach (var key in keys[..^1]) parent = parent[key]!;
        parent.AsObject().Remove(keys[^1]);
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json.ToJsonString(), Guid.Parse(run.RunId)));
    }

    [Theory]
    [InlineData("\"schema_version\":1", "\"schema_version\":1,\"schema_version\":1")]
    [InlineData("\"ram_gb\":32", "\"ram_gb\":32,\"ram_gb\":32")]
    [InlineData("\"weather\":\"clear\"", "\"weather\":\"clear\",\"weather\":\"clear\"")]
    [InlineData("\"VSync\":false", "\"VSync\":false,\"VSync\":false")]
    public void DuplicateKeysAreRejectedBeforeDeserialization(string original, string duplicate)
    {
        var run = SubmissionTests.Run();
        var json = SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070");
        Assert.Contains(original, json);
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.ValidateStored(json.Replace(original, duplicate), Guid.Parse(run.RunId)));
    }

    [Fact]
    public void IntegralResolutionNumbersDoNotRequireCanonicalJsonSpelling()
    {
        var run = SubmissionTests.Run();
        var json = SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070")
            .Replace("\"width\":2560", "\"width\":2.56e3").Replace("\"Width\":2560", "\"Width\":2560.0");
        SubmissionPayload.ValidateStored(json, Guid.Parse(run.RunId));
    }

    [Fact]
    public void ValidationAndRestartPreserveFrozenUtf8BytesIncludingOrderingWhitespaceAndEscapes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "frozen-request-review-" + Guid.NewGuid().ToString("N"));
        try
        {
            SubmissionOutbox Outbox() => new(directory, new("https://academy.example/api/bench/v1"), "https://fixture.clerk.accounts.dev", "fixture-client");
            var run = SubmissionTests.Run();
            var prepared = Outbox().Prepare(run, "NVIDIA GeForce RTX 4070");
            var root = JsonNode.Parse(prepared.Json)!.AsObject();
            var reordered = new JsonObject();
            foreach (var property in root.Reverse()) reordered[property.Key] = property.Value?.DeepClone();
            var frozen = " \r\n" + reordered.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("NVIDIA", "NV\\u0049DIA") + "\r\n\t";
            var bytes = Encoding.UTF8.GetBytes(frozen);
            var file = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single();
            File.WriteAllBytes(file, bytes);
            SubmissionPayload.ValidateStored(frozen, Guid.Parse(run.RunId));
            var restarted = Outbox().Prepare(run with { AppVersion = "changed" }, "changed GPU");
            Assert.Equal(frozen, restarted.Json);
            Assert.Equal(bytes, Encoding.UTF8.GetBytes(restarted.Json));
            Assert.Equal(bytes, File.ReadAllBytes(file));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
