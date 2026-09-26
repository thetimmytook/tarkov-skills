using System.Net;
using System.Net.Sockets;
using System.Text;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class AcademyApiTests
{
    private static readonly Uri Endpoint = new("https://academy.example/api/bench/v1");
    private static readonly Guid RunId = Guid.Parse("822a74ef-fd38-4fb4-8159-e37d0eeb8110");
    private static DesktopCredential Credential(string token = "fixture-access", bool expired = false) => new()
    {
        AccessToken = token, RefreshToken = "fixture-refresh",
        ExpiresAt = expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task RestoresSavedCredentialWithoutBrowserAndSendsOnlyBearerToExactLookup()
    {
        var store = new Store { Value = Credential() };
        using var session = Session(store, new Adapter());
        var handler = new Handler(HttpStatusCode.OK);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(DesktopAuthStatus.SignedIn, result.AuthStatus);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("{}", Encoding.UTF8.GetString(result.Content!));
        Assert.Equal(Endpoint + "/me/runs/by-client-id/" + RunId, handler.Url);
        Assert.Equal(new[] { "fixture-access" }, handler.Tokens);
        Assert.Null(handler.Request!.Headers.Authorization);
        Assert.DoesNotContain("fixture", result.ToString());
    }

    [Fact]
    public async Task RefreshesExpiredCredentialBeforeRequestAndPersistsRotation()
    {
        var store = new Store { Value = Credential(expired: true) };
        var adapter = new Adapter();
        using var session = Session(store, adapter);
        var handler = new Handler(HttpStatusCode.OK);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        Assert.Equal(HttpStatusCode.OK, (await client.GetRunAsync(RunId)).StatusCode);
        Assert.Equal(1, adapter.Refreshes);
        Assert.Equal(new[] { "fixture-rotated" }, handler.Tokens);
        Assert.Equal("fixture-rotated", store.Value!.AccessToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task RefreshesOnceOn401AndReturnsTheFinalServerStatus(HttpStatusCode final)
    {
        var store = new Store { Value = Credential() };
        var adapter = new Adapter();
        using var session = Session(store, adapter);
        var handler = new Handler(HttpStatusCode.Unauthorized, final);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(final, result.StatusCode);
        Assert.Equal(1, adapter.Refreshes);
        Assert.Equal(new[] { "fixture-access", "fixture-rotated" }, handler.Tokens);
        Assert.NotNull(store.Value);
        if (final == HttpStatusCode.Unauthorized) Assert.Null(result.Content);
    }

    [Fact]
    public async Task InvalidGrantAfter401ClearsCredentialAndDoesNotRetry()
    {
        var store = new Store { Value = Credential() };
        using var session = Session(store, new Adapter { IsRejected = true });
        var handler = new Handler(HttpStatusCode.Unauthorized);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(DesktopAuthStatus.SignInRequired, result.AuthStatus);
        Assert.Null(result.Content);
        Assert.Null(result.StatusCode);
        Assert.Null(store.Value);
        Assert.Single(handler.Tokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task OtherErrorsNeverRefreshRetryOrExposeErrorBodies(HttpStatusCode code)
    {
        var adapter = new Adapter();
        using var session = Session(new Store { Value = Credential() }, adapter);
        var handler = new Handler(code);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(code, result.StatusCode);
        Assert.Null(result.Content);
        Assert.Single(handler.Tokens);
        Assert.Equal(0, adapter.Refreshes);
    }

    [Fact]
    public async Task FailedRotationPersistenceRevokesNewGrantAndNeverUsesItForApi()
    {
        var store = new Store { Value = Credential(), FailSave = true };
        var adapter = new Adapter();
        using var session = Session(store, adapter);
        var handler = new Handler(HttpStatusCode.Unauthorized);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(DesktopAuthStatus.StorageUnavailable, result.AuthStatus);
        Assert.Null(result.Content);
        Assert.Null(result.StatusCode);
        Assert.Equal(1, adapter.Revocations);
        Assert.Single(handler.Tokens);
    }

    [Fact]
    public async Task RefreshOutageAfter401RetainsCredentialWithoutReplayingRequest()
    {
        var store = new Store { Value = Credential() };
        using var session = Session(store, new Adapter { IsOffline = true });
        var handler = new Handler(HttpStatusCode.Unauthorized);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(DesktopAuthStatus.Unavailable, result.AuthStatus);
        Assert.NotNull(store.Value);
        Assert.Null(result.StatusCode);
        Assert.Single(handler.Tokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrPendingLogoutCredentialNeverSends(bool isPending)
    {
        var store = new Store { Value = isPending ? Credential() : null };
        if (store.Value is not null) store.Value.RevocationPending = true;
        using var session = Session(store, new Adapter());
        var handler = new Handler(HttpStatusCode.OK);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(isPending ? DesktopAuthStatus.RevocationPending : DesktopAuthStatus.SignedOut, result.AuthStatus);
        Assert.Empty(handler.Tokens);
    }

    [Fact]
    public async Task NetworkFailureIsSanitizedWithoutRetryAndPreservesCredential()
    {
        var store = new Store { Value = Credential() };
        using var session = Session(store, new Adapter());
        var handler = new Handler() { IsOffline = true };
        using var client = new AcademyApiClient(Endpoint, session, handler);
        var result = await client.GetRunAsync(RunId);
        Assert.Equal(DesktopAuthStatus.Unavailable, result.AuthStatus);
        Assert.Null(result.Content);
        Assert.Null(result.StatusCode);
        Assert.NotNull(store.Value);
        Assert.Single(handler.Tokens);
        Assert.Null(handler.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task CanceledRequestDoesNotSendOrClearCredential()
    {
        var store = new Store { Value = Credential() };
        using var session = Session(store, new Adapter());
        var handler = new Handler(HttpStatusCode.OK);
        using var client = new AcademyApiClient(Endpoint, session, handler);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Equal(DesktopAuthStatus.Canceled, (await client.GetRunAsync(RunId, cancel.Token)).AuthStatus);
        Assert.Empty(handler.Tokens);
        Assert.NotNull(store.Value);
    }

    [Theory]
    [InlineData("http://academy.example/api/bench/v1", true)]
    [InlineData("http://127.0.0.1:8787/api/bench/v1", false)]
    [InlineData("https://user:pass@academy.example/api/bench/v1", false)]
    [InlineData("https://academy.example/api/bench/v1?redirect=other", false)]
    [InlineData("https://academy.example/api/admin/v1", false)]
    public void RejectsUnsafeEndpointConfiguration(string url, bool allowLocal)
    {
        using var session = Session(new Store(), new Adapter());
        Assert.Throws<ArgumentException>(() => new AcademyApiClient(new Uri(url), session, allowLocal));
    }

    [Fact]
    public async Task ProductionHttpHandlerDoesNotFollowRedirects()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var reader = new StreamReader(socket.GetStream());
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
            var reply = Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/unexpected\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await socket.GetStream().WriteAsync(reply, timeout.Token);
        });
        using var session = Session(new Store { Value = Credential() }, new Adapter());
        using var client = new AcademyApiClient(new Uri($"http://127.0.0.1:{port}/api/bench/v1"), session, true);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetRunAsync(RunId, timeout.Token)).StatusCode);
        await server;
        Assert.False(listener.Pending());
    }

    private static DesktopAuthSession Session(Store store, Adapter adapter) => new(
        new AuthConfiguration("https://fixture.clerk.accounts.dev", "fixture-client", DesktopAuthProduct.Benchmark),
        adapter, store, TimeProvider.System, _ => throw new InvalidOperationException("Unexpected browser launch"));

    private sealed class Store : ICredentialStore
    {
        public DesktopCredential? Value;
        public bool FailSave;
        public IDisposable AcquireLease() => new MemoryStream();
        public DesktopCredential? Load() => Value;
        public void Save(DesktopCredential credential)
        {
            if (FailSave) throw new IOException("private-storage-path");
            Value = credential;
        }
        public void Clear() => Value = null;
    }

    private sealed class Adapter : IDesktopAuthAdapter
    {
        public int Refreshes;
        public int Revocations;
        public bool IsRejected;
        public bool IsOffline;
        public Uri Authorize(PkceAttempt attempt, Uri redirect) => throw new NotSupportedException();
        public Task<DesktopCredential> ExchangeAsync(string code, string verifier, Uri redirect, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<DesktopCredential> RefreshAsync(DesktopCredential credential, CancellationToken cancellation)
        {
            Refreshes++;
            if (IsRejected) throw new CredentialRejected();
            if (IsOffline) throw new HttpRequestException("private-provider-details");
            return Task.FromResult(Credential("fixture-rotated"));
        }
        public Task RevokeAsync(DesktopCredential credential, CancellationToken cancellation)
        {
            Revocations++;
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public List<string> Tokens = [];
        public string? Url;
        public HttpRequestMessage? Request;
        public bool IsOffline;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Url = request.RequestUri!.AbsoluteUri;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            Tokens.Add(request.Headers.Authorization!.Parameter!);
            if (IsOffline) throw new HttpRequestException("private-url-and-token");
            var status = statuses[Tokens.Count - 1];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? "{}" : "private-provider-details")
            });
        }
    }
}
