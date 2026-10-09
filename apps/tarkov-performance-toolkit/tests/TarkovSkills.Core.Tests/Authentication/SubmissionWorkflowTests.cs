using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class SubmissionWorkflowTests
{
    [Theory]
    [InlineData("pending_review")]
    [InlineData("published")]
    [InlineData("rejected")]
    [InlineData("deleted")]
    public async Task LookupPersistsOnlyConfirmedStatusAndPreventsAnotherPost(string status)
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = _ => Owner(fixture.Id, status);
        var check = await fixture.Workflow.CheckAsync(fixture.Id);
        Assert.Equal(SubmissionOutcome.Confirmed, check.Outcome);
        Assert.Equal(status, fixture.NewOutbox().ReadStatus(fixture.Id)!.Status);
        var result = await fixture.Workflow.SubmitAsync(fixture.Prepared);
        Assert.Equal(status, result.Checkpoint!.Status);
        Assert.All(fixture.Handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
        var cached = File.ReadAllText(Directory.GetFiles(fixture.Directory, "*.status.json", SearchOption.AllDirectories).Single());
        Assert.DoesNotContain("fixture-access", cached);
        Assert.DoesNotContain("hardware", cached);
        Assert.DoesNotContain("public_run_id", cached);
        Assert.DoesNotContain("resource_telemetry", cached);
    }

    [Fact]
    public async Task ModeratorDecisionReplacesPendingAndDeletionCannotBeRevertedByAnOlderReply()
    {
        using var fixture = new Fixture();
        foreach (var status in new[] { "pending_review", "published", "deleted" })
        {
            fixture.Handler.Reply = _ => Owner(fixture.Id, status);
            Assert.Equal(status, (await fixture.Workflow.CheckAsync(fixture.Id)).Checkpoint!.Status);
        }
        fixture.Handler.Reply = _ => Owner(fixture.Id, "published");
        Assert.Equal("deleted", (await fixture.Workflow.CheckAsync(fixture.Id)).Checkpoint!.Status);
        fixture.Handler.Methods.Clear();
        Assert.Equal("deleted", (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Checkpoint!.Status);
        Assert.Empty(fixture.Handler.Methods);
    }

    [Fact]
    public async Task UncertainPostIsResolvedByLookupOnNextExplicitActionWithoutReposting()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = request => request.Method == HttpMethod.Get
            ? new(HttpStatusCode.NotFound) : throw new HttpRequestException("private network details");
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, fixture.Handler.Methods);
        fixture.Handler.Reply = _ => Owner(fixture.Id, "pending_review");
        Assert.Equal("pending_review", (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Checkpoint!.Status);
        Assert.Equal(1, fixture.Handler.Methods.Count(method => method == HttpMethod.Post));
    }

    [Fact]
    public async Task MissingAfterUncertainPostReusesExactPayloadAcrossRestart()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = request => request.Method == HttpMethod.Get
            ? new(HttpStatusCode.NotFound) : throw new HttpRequestException();
        await fixture.Workflow.SubmitAsync(fixture.Prepared);
        fixture.Handler.Reply = request => request.Method == HttpMethod.Get
            ? new(HttpStatusCode.NotFound) : Receipt(fixture.Id, "pending_review");
        var restored = fixture.NewOutbox().Prepare(SubmissionTests.Run() with { AppVersion = "changed" }, "ignored");
        var result = await new SubmissionWorkflow(fixture.Api, fixture.NewOutbox()).SubmitAsync(restored);
        Assert.Equal("pending_review", result.Checkpoint!.Status);
        Assert.Equal(new[] { fixture.Prepared.Json, fixture.Prepared.Json }, fixture.Handler.Bodies);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task LookupFailureNeverTriggersPostOrErasesLastKnownStatus(int code)
    {
        using var fixture = new Fixture();
        fixture.Outbox.SaveStatus(fixture.Id, "pending_review");
        fixture.Handler.Reply = _ => new((HttpStatusCode)code);
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        Assert.DoesNotContain(HttpMethod.Post, fixture.Handler.Methods);
        Assert.Equal("pending_review", fixture.Outbox.ReadStatus(fixture.Id)!.Status);
    }

    [Fact]
    public async Task KnownRunReturning404DoesNotPublishAgainUnderAnotherAccount()
    {
        using var fixture = new Fixture();
        fixture.Outbox.SaveStatus(fixture.Id, "published");
        fixture.Handler.Reply = _ => new(HttpStatusCode.NotFound);
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        Assert.Equal(new[] { HttpMethod.Get }, fixture.Handler.Methods);
        Assert.Equal("published", fixture.Outbox.ReadStatus(fixture.Id)!.Status);
    }

    [Fact]
    public async Task SharedLoginSwitchBetweenLookupAndPostStopsSubmission()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = _ =>
        {
            fixture.Store.Save(Credential("fixture-other-account"));
            return new(HttpStatusCode.NotFound);
        };
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        Assert.Equal(new[] { HttpMethod.Get }, fixture.Handler.Methods);
    }

    [Theory]
    [InlineData("publication_deleted", SubmissionOutcome.Confirmed)]
    [InlineData("idempotency_conflict", SubmissionOutcome.Conflict)]
    [InlineData("private-provider-value", SubmissionOutcome.Unconfirmed)]
    public async Task ConflictCodesAreAllowlistedAndDeletionSurvivesRestart(string code, SubmissionOutcome outcome)
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = request => request.Method == HttpMethod.Get ? new(HttpStatusCode.NotFound) :
            new(HttpStatusCode.Conflict) { Content = new StringContent(new JsonObject { ["code"] = code, ["message"] = "private-provider-details" }.ToJsonString()) };
        Assert.Equal(outcome, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        if (code == "publication_deleted") Assert.Equal("deleted", fixture.NewOutbox().ReadStatus(fixture.Id)!.Status);
        else Assert.Null(fixture.NewOutbox().ReadStatus(fixture.Id));
    }

    [Fact]
    public async Task WrongRunAndMalformedResponsesNeverConfirmOrResubmit()
    {
        using var fixture = new Fixture();
        fixture.Handler.Reply = _ => Owner(Guid.NewGuid(), "published");
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        fixture.Handler.Reply = _ => new(HttpStatusCode.OK) { Content = new StringContent("invalid") };
        Assert.Equal(SubmissionOutcome.Unconfirmed, (await fixture.Workflow.CheckAsync(fixture.Id)).Outcome);
        Assert.Null(fixture.Outbox.ReadStatus(fixture.Id));
        Assert.DoesNotContain(HttpMethod.Post, fixture.Handler.Methods);
    }

    [Fact]
    public async Task CorruptLocalCheckpointBlocksSubmission()
    {
        using var fixture = new Fixture();
        fixture.Outbox.SaveStatus(fixture.Id, "pending_review");
        File.WriteAllText(Directory.GetFiles(fixture.Directory, "*.status.json", SearchOption.AllDirectories).Single(), "invalid");
        Assert.Equal(SubmissionOutcome.StorageUnavailable, (await fixture.Workflow.SubmitAsync(fixture.Prepared)).Outcome);
        Assert.Empty(fixture.Handler.Methods);
    }

    private static HttpResponseMessage Owner(Guid id, string status)
    {
        var item = ReceiptBody(id, status);
        if (status != "deleted")
        {
            item["submitted_at"] = "2026-09-26T12:00:00Z";
            item["captured_day"] = "2026-09-26";
            item["hardware"] = new JsonObject { ["cpu"] = "fixture CPU", ["gpu"] = "fixture GPU", ["ram_gb"] = 32 };
            item["map"] = new JsonObject { ["id"] = "woods", ["name"] = "Woods" };
            item["execution"] = "bsg_servers";
            item["game_resolution"] = null;
            item["metrics"] = new JsonObject { ["average_fps"] = 50, ["one_percent_low_fps"] = 40 };
            var resources = System.Text.Json.JsonSerializer.SerializeToNode(ResourceSubmissionFixtures.Collected(), JsonDefaults.Options)!;
            resources["cpu"]!.AsObject().Remove("logical_processors");
            resources["pagefile"]!.AsObject().Remove("files");
            item["resource_telemetry"] = resources;
            item["status_reason"] = status == "rejected" ? "rejected" : null;
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["item"] = item }.ToJsonString()) };
    }
    private static HttpResponseMessage Receipt(Guid id, string status) => new(status == "pending_review" ? HttpStatusCode.Accepted : HttpStatusCode.OK)
    { Content = new StringContent(ReceiptBody(id, status).ToJsonString()) };
    private static JsonObject ReceiptBody(Guid id, string status) => new()
    {
        ["client_run_id"] = id.ToString(), ["publication_status"] = status,
        ["public_run_id"] = status == "published" ? "br_fixture" : null,
        ["url"] = status == "published" ? "/bench/runs/br_fixture" : null
    };
    private static DesktopCredential Credential(string token = "fixture-access") => new()
    { AccessToken = token, RefreshToken = "fixture-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "academy-status-test-" + Guid.NewGuid());
        public Store Store { get; } = new();
        public Handler Handler { get; } = new();
        private readonly DesktopAuthSession session;
        public AcademyApiClient Api { get; }
        public SubmissionOutbox Outbox { get; }
        public SubmissionWorkflow Workflow { get; }
        public PreparedSubmission Prepared { get; }
        public Guid Id => Prepared.ClientRunId;
        public SubmissionOutbox NewOutbox() => new(Directory, new("https://academy.example/api/bench/v1"), "https://fixture.clerk.accounts.dev", "fixture-client");
        public Fixture()
        {
            session = new(new("https://fixture.clerk.accounts.dev", "fixture-client", DesktopAuthProduct.Benchmark), new Adapter(), Store, TimeProvider.System, _ => throw new InvalidOperationException());
            Api = new(new("https://academy.example/api/bench/v1"), session, Handler);
            Outbox = NewOutbox();
            Prepared = Outbox.Prepare(ResourceSubmissionFixtures.Run(ResourceSubmissionFixtures.Collected(partial: true)), "NVIDIA GeForce RTX 4070");
            Workflow = new(Api, Outbox);
        }
        public void Dispose() { Api.Dispose(); session.Dispose(); System.IO.Directory.Delete(Directory, true); }
    }
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
    private sealed class Handler : HttpMessageHandler
    {
        public List<HttpMethod> Methods = [];
        public List<string> Bodies = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Reply = _ => new(HttpStatusCode.NotFound);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return Reply(request);
        }
    }
}
