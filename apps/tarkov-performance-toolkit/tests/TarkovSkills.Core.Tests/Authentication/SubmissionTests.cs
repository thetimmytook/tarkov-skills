using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class SubmissionTests
{
    private static readonly Guid Id = Guid.Parse("871e06df-5c0f-447a-a3b7-723492a2e09e");
    private static readonly Uri Endpoint = new("https://academy.example/api/bench/v1");
    internal static BenchmarkRun Run(string map = "Woods") => new(Id.ToString(), "2026-09-26", 120, "1.0.3",
        new { cpu = new { name = "AMD Ryzen 7 7800X3D", serial_number = "private-serial" },
            gpu = new[] { new { name = "NVIDIA GeForce RTX 4070", current_resolution = "1920x1080" } },
            ram = new { total_gb = 32 }, computer_name = "private-machine", email = "private@example.com" },
        JsonNode.Parse("""
        {"graphics":{"DisplaySettings":{"Resolution":{"Width":2560,"Height":1440},"FullScreenMode":1,"Display":7},
          "TextureQuality":2,"VSync":false,"DLSSMode":"Off","Stored":[{"private":"private-monitor"}],"unknown":"private-path"},
         "game":{"AutoEmptyWorkingSet":false,"SetAffinityToLogicalCores":true,"private":"private-account"},
         "postfx":{"EnablePostFx":false,"Brightness":99},"private":"private-secret"}
        """)!,
        new { map, execution = "bsg_servers", weather = "clear", time_of_day = "day", game_version = "1.0.0.0", email = "private@example.com" },
        new PerformanceMetrics(6000, 120, 50, 40, 30, 20, 25, 35), ["private-warning"], Submitted: true);

    [Fact]
    public void ProjectionUsesOnlyApprovedFieldsAndPreservesFalseAndGameResolution()
    {
        var json = SubmissionPayload.Create(Run(), "NVIDIA GeForce RTX 4070");
        Assert.DoesNotContain("private", json);
        Assert.DoesNotContain("submitted", json);
        Assert.DoesNotContain("1920", json);
        Assert.DoesNotContain("Brightness", json);
        var dto = JsonNode.Parse(json)!;
        Assert.False(dto["settings_snapshot"]!["graphics"]!["VSync"]!.GetValue<bool>());
        Assert.Equal(2560, dto["game_resolution"]!["width"]!.GetValue<int>());
        Assert.Equal(120, dto["capture"]!["duration_sec"]!.GetValue<double>());
        Assert.Equal(Id.ToString(), dto["client_run_id"]!.GetValue<string>());
        SubmissionPayload.ValidateStored(json, Id);
        // Optional export of synthetic fixtures for checking against the actual TS contract.
        var export = Environment.GetEnvironmentVariable("ACADEMY_CONTRACT_TEST_OUTPUT");
        if (!string.IsNullOrEmpty(export)) File.WriteAllText(export, json);
    }

    [Theory]
    [InlineData("Factory", "factory")]
    [InlineData("The Lab", "the-lab")]
    [InlineData("Reserve", "reserve")]
    [InlineData("Ground Zero", "ground-zero")]
    [InlineData("Interchange", "interchange")]
    [InlineData("Shoreline", "shoreline")]
    [InlineData("Labyrinth", "labyrinth")]
    [InlineData("Streets of Tarkov", "streets")]
    [InlineData("Customs", "customs")]
    [InlineData("Lighthouse", "lighthouse")]
    [InlineData("Woods", "woods")]
    public void AllAgreedMapsUseCanonicalIds(string map, string expected)
    {
        var json = SubmissionPayload.Create(Run(map), "NVIDIA GeForce RTX 4070");
        Assert.Equal(expected, JsonNode.Parse(json)!["map"]!.GetValue<string>());
        SubmissionPayload.ValidateStored(json, Id);
    }

    [Fact]
    public void MeasuredDurationIsUsedInsteadOfRequestedDuration()
    {
        var run = Run() with { DurationSec = 240 };
        Assert.Equal(120, JsonNode.Parse(SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070"))!["capture"]!["duration_sec"]!.GetValue<double>());
    }

    [Fact]
    public void MissingReviewedSettingsRemainNullAndNeverUseMonitorResolution()
    {
        var run = Run() with { Settings = new { private_key = "private" } };
        var dto = JsonNode.Parse(SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070"))!;
        Assert.Null(dto["game_resolution"]);
        Assert.Null(dto["settings_snapshot"]);
        SubmissionPayload.ValidateStored(dto.ToJsonString(), Id);
    }

    [Fact]
    public void InvalidAndInconsistentMetricsAreRejectedRatherThanRepaired()
    {
        foreach (var metrics in new[] {
            Run().Performance with { DurationSec = 109 }, Run().Performance with { SampleCount = 119 },
            Run().Performance with { AverageFps = 60 }, Run().Performance with { OnePercentLowFps = 70 },
            Run().Performance with { AverageFrametimeMs = double.NaN } })
            Assert.Throws<InvalidDataException>(() => SubmissionPayload.Create(Run() with { Performance = metrics }, "NVIDIA GeForce RTX 4070"));
    }

    [Fact]
    public void SettingsValuesCannotCarryFreeTextOrPaths()
    {
        var run = Run() with { Settings = JsonNode.Parse("""{"graphics":{"DLSSMode":"private@example.com"}}""")! };
        Assert.Throws<InvalidDataException>(() => SubmissionPayload.Create(run, "NVIDIA GeForce RTX 4070"));
    }

    [Fact]
    public void FrozenPayloadSurvivesRestartAndChangesToLocalRunOrSelection()
    {
        WithDirectory(path => {
            var outbox = Outbox(path);
            var first = outbox.Prepare(Run(), "NVIDIA GeForce RTX 4070");
            var second = Outbox(path).Prepare(Run() with { AppVersion = "changed", Settings = new { unknown = "private" } }, "changed GPU");
            Assert.Equal(first.Json, second.Json);
            Assert.Equal(Id, second.ClientRunId);
            var file = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Single();
            var modified = JsonNode.Parse(File.ReadAllText(file))!;
            modified["email"] = "private@example.com";
            File.WriteAllText(file, modified.ToJsonString());
            Assert.Throws<InvalidDataException>(() => Outbox(path).Prepare(Run(), "NVIDIA GeForce RTX 4070"));
        });
    }

    [Theory]
    [InlineData("pending_review", 202)]
    [InlineData("published", 200)]
    [InlineData("rejected", 200)]
    public void OnlyValidatedServerReceiptConfirmsSubmission(string status, int code)
    {
        var json = new JsonObject { ["client_run_id"] = Id.ToString(), ["publication_status"] = status,
            ["public_run_id"] = status == "published" ? "br_fixture" : null, ["url"] = status == "published" ? "/bench/runs/br_fixture" : null };
        if (status == "rejected") json["status_reason"] = "rejected";
        AcademyApiResult Result() => new(DesktopAuthStatus.SignedIn, (HttpStatusCode)code, Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Equal(status, SubmissionReceipt.Read(Result(), Id));
        Assert.Null(SubmissionReceipt.Read(Result(), Guid.NewGuid()));
        json["url"] = "https://foreign.example/private";
        Assert.Null(SubmissionReceipt.Read(Result(), Id));
    }

    [Fact]
    public async Task Post401ReplaysTheExactFrozenRequestAndOnlyExplicitCallsSend()
    {
        var path = Path.Combine(Path.GetTempPath(), "academy-submit-test-" + Guid.NewGuid());
        try
        {
            var prepared = Outbox(path).Prepare(Run(), "NVIDIA GeForce RTX 4070");
            var handler = new SubmitHandler();
            using var session = new DesktopAuthSession(new("https://fixture.clerk.accounts.dev", "fixture-client", DesktopAuthProduct.Benchmark),
                new Adapter(), new Store(), TimeProvider.System, _ => throw new InvalidOperationException());
            using var client = new AcademyApiClient(Endpoint, session, handler);
            Assert.Empty(handler.Bodies);
            var result = await client.SubmitAsync(prepared);
            Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
            Assert.Equal(new[] { prepared.Json, prepared.Json }, handler.Bodies);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static SubmissionOutbox Outbox(string path) => new(path, Endpoint, "https://fixture.clerk.accounts.dev", "fixture-client");
    private static void WithDirectory(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "academy-outbox-test-" + Guid.NewGuid());
        try { action(path); } finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    private static DesktopCredential Credential() => new() { AccessToken = "fixture-access", RefreshToken = "fixture-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
    private sealed class Store : ICredentialStore
    {
        private DesktopCredential? value = Credential();
        public IDisposable AcquireLease() => new MemoryStream();
        public DesktopCredential? Load() => value;
        public void Save(DesktopCredential credential) => value = credential;
        public void Clear() => value = null;
    }
    private sealed class Adapter : IDesktopAuthAdapter
    {
        public Uri Authorize(PkceAttempt attempt, Uri redirect) => throw new NotSupportedException();
        public Task<DesktopCredential> ExchangeAsync(string code, string verifier, Uri redirect, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<DesktopCredential> RefreshAsync(DesktopCredential credential, CancellationToken cancellation) => Task.FromResult(Credential());
        public Task RevokeAsync(DesktopCredential credential, CancellationToken cancellation) => Task.CompletedTask;
    }
    private sealed class SubmitHandler : HttpMessageHandler
    {
        public List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Endpoint + "/me/runs", request.RequestUri!.AbsoluteUri);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Bodies.Count == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.Accepted);
        }
    }
}
