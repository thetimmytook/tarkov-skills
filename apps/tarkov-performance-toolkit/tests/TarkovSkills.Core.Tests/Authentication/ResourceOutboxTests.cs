using System.Text;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using static TarkovSkills.Core.Tests.Authentication.ResourceSubmissionFixtures;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class ResourceOutboxTests
{
    [Fact]
    public void LargeNewSummaryAboveOldLimitPersistsAndReloads()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Large());
            var prepared = outbox.Prepare(run, GpuName);
            Assert.InRange(Encoding.UTF8.GetByteCount(prepared.Json), 32769, SubmissionPayload.MaxPayloadBytes);
            Assert.Equal(prepared.Json, outbox.Prepare(run, GpuName).Json);
            Assert.Equal(128, prepared.ResourceTelemetry.Cpu.LogicalProcessors.Count);
            ResourceSubmissionTests.Export("large-128", prepared.Json);
        });
    }

    [Fact]
    public void OversizedNewSummaryDoesNotPersistARequest()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Large(512, manyReasons: true));
            var json = SubmissionPayload.Create(run, GpuName);
            Assert.True(Encoding.UTF8.GetByteCount(json) > SubmissionPayload.MaxPayloadBytes);
            Assert.Throws<InvalidDataException>(() => outbox.Prepare(run, GpuName));
            Assert.Empty(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
        });
    }

    [Fact]
    public void SavedPayloadAtExactByteLimitReloadsButAnExtraByteFailsWithoutRewriting()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Collected());
            var prepared = outbox.Prepare(run, GpuName);
            var path = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single();
            var exact = prepared.Json + new string(' ', SubmissionPayload.MaxPayloadBytes - Encoding.UTF8.GetByteCount(prepared.Json));
            File.WriteAllText(path, exact);
            Assert.Equal(SubmissionPayload.MaxPayloadBytes, new FileInfo(path).Length);
            Assert.Equal(exact, outbox.Prepare(run, GpuName).Json);
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidDataException>(() => outbox.Prepare(run, GpuName));
            Assert.Equal(exact + " ", File.ReadAllText(path));
        });
    }

    [Fact]
    public void InvalidSavedEncodingFailsWithoutReplacingCharactersOrRewritingTheRequest()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Collected());
            var prepared = outbox.Prepare(run, GpuName);
            var path = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single();
            var bytes = Encoding.UTF8.GetBytes(prepared.Json);
            var index = prepared.Json.IndexOf("NVIDIA", StringComparison.Ordinal);
            bytes[index] = 0xC3; bytes[index + 1] = 0x28; // An invalid UTF-8 continuation.
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => outbox.Prepare(run, GpuName));
            Assert.Equal(bytes, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void OldFrozenPayloadIsExplicitlyUnsupportedAndHistoryStatusRemainUntouched()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Collected());
            outbox.Prepare(run, GpuName);
            var path = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single();
            var json = JsonNode.Parse(File.ReadAllText(path))!; json.AsObject().Remove("resource_telemetry");
            var legacy = json.ToJsonString(); File.WriteAllText(path, legacy);
            outbox.SaveStatus(Guid.Parse(run.RunId), "published");
            var exception = Assert.Throws<InvalidDataException>(() => outbox.Prepare(run, GpuName));
            Assert.Equal(SubmissionPayload.UnsupportedSavedPayload, exception.Message);
            Assert.Equal(legacy, File.ReadAllText(path));
            Assert.Equal("published", outbox.ReadStatus(Guid.Parse(run.RunId))!.Status);
            Assert.Equal(run.RunId, json["client_run_id"]!.GetValue<string>());
        });
    }

    [Fact]
    public void FrozenSummarySurvivesRestartAndDifferentLocalTelemetry()
    {
        WithOutbox((directory, outbox) =>
        {
            var run = Run(Collected(partial: true));
            var first = outbox.Prepare(run, GpuName);
            var restarted = Outbox(directory).Prepare(run with { AppVersion = "changed", ResourceTelemetry = Collected(unified: true) }, "changed GPU");
            Assert.Equal(first.Json, restarted.Json);
            Assert.Equal("partial", restarted.ResourceTelemetry.Status);
            Assert.Equal("discrete", restarted.ResourceTelemetry.Gpu.MemoryArchitecture);
            Assert.Equal(first.Summary, restarted.Summary);
            Assert.Contains("whole adapter", restarted.Summary);
        });
    }

    [Theory]
    [InlineData(GpuUsageSources.Windows, GpuUsageSources.Windows)]
    [InlineData(GpuUsageSources.Windows, GpuUsageSources.Nvidia)]
    [InlineData(GpuUsageSources.Windows, GpuUsageSources.Amd)]
    [InlineData(GpuUsageSources.Nvidia, GpuUsageSources.Windows)]
    [InlineData(GpuUsageSources.Nvidia, GpuUsageSources.Nvidia)]
    [InlineData(GpuUsageSources.Nvidia, GpuUsageSources.Amd)]
    [InlineData(GpuUsageSources.Amd, GpuUsageSources.Windows)]
    [InlineData(GpuUsageSources.Amd, GpuUsageSources.Nvidia)]
    [InlineData(GpuUsageSources.Amd, GpuUsageSources.Amd)]
    public void FrozenSourceAndExactBytesSurviveAllSourceChangesAndRestart(string firstSource, string laterSource)
    {
        WithOutbox((directory, outbox) =>
        {
            var telemetry = Collected();
            var firstTelemetry = telemetry with { Gpu = telemetry.Gpu with { GraphicsUtilization =
                telemetry.Gpu.GraphicsUtilization with { Source = firstSource } } };
            var run = Run(firstTelemetry);
            var first = outbox.Prepare(run, GpuName);
            var file = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single();
            var frozenBytes = File.ReadAllBytes(file);
            var laterTelemetry = firstTelemetry with { Gpu = firstTelemetry.Gpu with { GraphicsUtilization =
                firstTelemetry.Gpu.GraphicsUtilization with { Source = laterSource, Average = 50, Minimum = 50, Maximum = 50, Last = 50 } } };
            var restarted = Outbox(directory).Prepare(run with { ResourceTelemetry = laterTelemetry }, GpuName);
            Assert.Equal(first.Json, restarted.Json);
            Assert.Equal(firstSource, restarted.ResourceTelemetry.Gpu.GraphicsUtilization.Source);
            Assert.Equal(firstTelemetry.Gpu.GraphicsUtilization.Average, restarted.ResourceTelemetry.Gpu.GraphicsUtilization.Average);
            Assert.Equal(frozenBytes, File.ReadAllBytes(file));
        });
    }

    private static SubmissionOutbox Outbox(string directory) => new(directory, new("https://academy.example/api/bench/v1"), "https://fixture.clerk.accounts.dev", "fixture-client");
    private static void WithOutbox(Action<string, SubmissionOutbox> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "academy-resource-outbox-" + Guid.NewGuid());
        try { test(directory, Outbox(directory)); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
