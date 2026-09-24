using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

// Real adapter + DPAPI store + two product sessions. Only HTTP and time are synthetic.
public sealed class RecoveryTests
{
    [Fact]
    public async Task ExpiringCredentialSurvivesNetworkFailureThenRotatesOnceForBothApps()
    {
        using var fixture = new Fixture();
        var requests = 0;
        fixture.Respond = async request =>
        {
            requests++;
            Assert.Equal("/oauth/token", request.RequestUri!.AbsolutePath);
            Assert.Empty(request.RequestUri.Query);
            var body = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
            Assert.Equal("refresh_token", body["grant_type"]);
            Assert.Equal("fixture-refresh", body["refresh_token"]);
            if (requests == 1) throw new HttpRequestException("fixture-network-outage");
            return Json(HttpStatusCode.OK, new { token_type = "Bearer", access_token = "fixture-rotated-access", refresh_token = "fixture-rotated-refresh", expires_in = 3600 });
        };
        fixture.Store.Save(fixture.Credential());
        using var benchmark = fixture.Session(DesktopAuthProduct.Benchmark);
        using var toolkit = fixture.Session(DesktopAuthProduct.Toolkit);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(59);
        Assert.Equal(DesktopAuthStatus.SignedIn, await benchmark.RestoreAsync());
        Assert.Equal(0, requests); // 61 seconds left; no early refresh.
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        Assert.Equal(DesktopAuthStatus.Unavailable, await benchmark.RestoreAsync());
        Assert.Equal("fixture-refresh", fixture.Store.Load()!.RefreshToken);
        var statuses = await Task.WhenAll(benchmark.RestoreAsync(), toolkit.RestoreAsync());
        Assert.All(statuses, status => Assert.Equal(DesktopAuthStatus.SignedIn, status));
        Assert.Equal(2, requests); // One failed request and one successful shared refresh.
        Assert.Equal("fixture-rotated-refresh", fixture.Store.Load()!.RefreshToken);
        using var restarted = fixture.Session(DesktopAuthProduct.Toolkit);
        Assert.Equal(DesktopAuthStatus.SignedIn, await restarted.RestoreAsync());
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task InvalidGrantRemovesSharedCredentialAndBothAppsRequireSignIn()
    {
        using var fixture = new Fixture();
        fixture.Respond = _ => Task.FromResult(Json(HttpStatusCode.BadRequest,
            new { error = "invalid_grant", error_description = "fixture-private-description" }));
        fixture.Store.Save(fixture.Credential());
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(3);
        using var benchmark = fixture.Session(DesktopAuthProduct.Benchmark);
        using var toolkit = fixture.Session(DesktopAuthProduct.Toolkit);
        Assert.Equal(DesktopAuthStatus.SignInRequired, await toolkit.RestoreAsync());
        Assert.Equal(DesktopAuthStatus.SignedOut, await benchmark.RestoreAsync());
        Assert.Null(fixture.Store.Load());
    }

    [Fact]
    public async Task PartialRevocationPersistsAcrossRestartAndCanBeRetriedByOtherApp()
    {
        using var fixture = new Fixture();
        var hints = new List<string>();
        fixture.Respond = async request =>
        {
            Assert.Equal("/oauth/token/revoke", request.RequestUri!.AbsolutePath);
            Assert.Empty(request.RequestUri.Query);
            var body = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
            hints.Add(body["token_type_hint"].ToString());
            return hints.Count == 2
                ? Json(HttpStatusCode.ServiceUnavailable, new { error = "fixture-outage" })
                : new HttpResponseMessage(HttpStatusCode.OK);
        };
        fixture.Store.Save(fixture.Credential());
        using (var benchmark = fixture.Session(DesktopAuthProduct.Benchmark))
            Assert.Equal(DesktopAuthStatus.RevocationPending, await benchmark.SignOutAsync());
        Assert.True(fixture.Store.Load()!.RevocationPending);
        using var restarted = fixture.Session(DesktopAuthProduct.Toolkit);
        Assert.Equal(DesktopAuthStatus.RevocationPending, await restarted.RestoreAsync());
        Assert.Equal(DesktopAuthStatus.RevocationPending, await restarted.SignInAsync());
        Assert.Equal(2, hints.Count);
        Assert.Equal(DesktopAuthStatus.SignedOut, await restarted.SignOutAsync());
        Assert.Equal(new[] { "refresh_token", "access_token", "refresh_token", "access_token" }, hints);
        Assert.Null(fixture.Store.Load());
        using var other = fixture.Session(DesktopAuthProduct.Benchmark);
        Assert.Equal(DesktopAuthStatus.SignedOut, await other.RestoreAsync());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body)) };

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "tarkov-auth-recovery-" + Guid.NewGuid().ToString("N"));
        private readonly HttpClient http;
        public Clock Clock { get; } = new();
        public CredentialStore Store { get; }
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond = _ => throw new InvalidOperationException("Unexpected request");

        public Fixture()
        {
            http = new HttpClient(new Handler(this));
            Store = new CredentialStore(Config(DesktopAuthProduct.Benchmark), directory);
        }

        private static AuthConfiguration Config(DesktopAuthProduct product) => new("https://fixture.clerk.accounts.dev", "fixture-client", product);
        public DesktopCredential Credential() => new() { AccessToken = "fixture-access", RefreshToken = "fixture-refresh", ExpiresAt = Clock.Now.AddMinutes(2) };
        public DesktopAuthSession Session(DesktopAuthProduct product)
        {
            var config = Config(product);
            return new(config, new ClerkDesktopAdapter(config, http, Clock), new CredentialStore(config, directory), Clock,
                _ => throw new Xunit.Sdk.XunitException("Recovery must not open the browser"));
        }

        public void Dispose()
        {
            http.Dispose();
            Directory.Delete(directory, recursive: true);
        }

        private sealed class Handler(Fixture fixture) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => fixture.Respond(request);
        }
    }
}
