using Microsoft.AspNetCore.WebUtilities;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class SessionTests
{
    private static readonly AuthConfiguration Config = new("https://fixture.clerk.accounts.dev", "fixture-client", DesktopAuthProduct.Benchmark);
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RestartRestoresUnexpiredCredentialWithoutBrowserOrNetwork()
    {
        var store = new MemoryStore { Value = Credential() };
        using var session = Session(store, new Adapter());
        Assert.Equal(DesktopAuthStatus.SignedIn, await session.RestoreAsync());
    }

    [Fact]
    public async Task ConcurrentExpiryChecksRefreshOnceAndPersistRotatedCredentialForRestart()
    {
        var store = new MemoryStore { Value = Credential(expired: true) };
        var adapter = new Adapter();
        using var session = Session(store, adapter);
        var statuses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => session.RestoreAsync()));
        Assert.All(statuses, status => Assert.Equal(DesktopAuthStatus.SignedIn, status));
        Assert.Equal(1, adapter.Refreshes);
        Assert.Equal("fixture-new-refresh", store.Value!.RefreshToken);
        using var restarted = Session(store, new Adapter());
        Assert.Equal(DesktopAuthStatus.SignedIn, await restarted.RestoreAsync());
    }

    [Fact]
    public async Task InvalidGrantClearsSavedCredentialAndRequiresNewSignIn()
    {
        var store = new MemoryStore { Value = Credential(expired: true) };
        using var session = Session(store, new Adapter { RefreshError = new CredentialRejected() });
        Assert.Equal(DesktopAuthStatus.SignInRequired, await session.RestoreAsync());
        Assert.Null(store.Value);
        Assert.Equal(DesktopAuthStatus.SignedOut, await session.RestoreAsync());
    }

    [Fact]
    public async Task OutageRetainsExpiredCredentialWithoutReportingSignedIn()
    {
        var original = Credential(expired: true);
        var store = new MemoryStore { Value = original };
        using var session = Session(store, new Adapter { RefreshError = new HttpRequestException("private-provider-value") });
        Assert.Equal(DesktopAuthStatus.Unavailable, await session.RestoreAsync());
        Assert.Same(original, store.Value);
    }

    [Fact]
    public async Task FailedLogoutPersistsIntentAcrossRestartAndCannotRefreshOrSignInUntilRevoked()
    {
        var store = new MemoryStore { Value = Credential() };
        var adapter = new Adapter { RevokeError = new HttpRequestException("private-provider-value") };
        using (var session = Session(store, adapter))
            Assert.Equal(DesktopAuthStatus.RevocationPending, await session.SignOutAsync());
        Assert.True(store.Value!.RevocationPending);
        using var restarted = Session(store, adapter);
        Assert.Equal(DesktopAuthStatus.RevocationPending, await restarted.RestoreAsync());
        Assert.Equal(DesktopAuthStatus.RevocationPending, await restarted.SignInAsync());
        Assert.Equal(0, adapter.Refreshes);
        adapter.RevokeError = null;
        Assert.Equal(DesktopAuthStatus.SignedOut, await restarted.SignOutAsync());
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task PersistenceFailureRevokesFreshCredentialAndReturnsOnlySafeStatus()
    {
        var store = new MemoryStore { Value = Credential(expired: true), FailSave = true };
        var adapter = new Adapter();
        using var session = Session(store, adapter);
        Assert.Equal(DesktopAuthStatus.StorageUnavailable, await session.RestoreAsync());
        Assert.Equal("fixture-new-refresh", adapter.Revoked!.RefreshToken);
    }

    [Fact]
    public async Task BrowserLoginUsesRealOneShotCallbackAndPersistsExchangeResult()
    {
        var store = new MemoryStore();
        var adapter = new Adapter();
        Task? browserRequest = null;
        using var http = new HttpClient();
        using var session = new DesktopAuthSession(Config, adapter, store, new Clock(), uri =>
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            browserRequest = http.GetStringAsync(query["redirect_uri"] + "?code=fixture-code&state=" + query["state"]);
        });
        Assert.Equal(DesktopAuthStatus.SignedIn, await session.SignInAsync());
        await browserRequest!;
        Assert.Equal("fixture-code", adapter.Code);
        Assert.NotNull(store.Value);
        Assert.Equal(43, adapter.Verifier!.Length);
    }

    [Fact]
    public async Task CancellationWhileWaitingForBrowserClosesSocketAndSavesNothing()
    {
        var store = new MemoryStore();
        using var cancel = new CancellationTokenSource();
        Uri? redirect = null;
        using var session = new DesktopAuthSession(Config, new Adapter(), store, new Clock(), uri =>
        {
            redirect = new(QueryHelpers.ParseQuery(uri.Query)["redirect_uri"].ToString());
            cancel.Cancel();
        });
        Assert.Equal(DesktopAuthStatus.Canceled, await session.SignInAsync(cancel.Token));
        Assert.Null(store.Value);
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, redirect!.Port);
        listener.Start();
    }

    [Fact]
    public async Task BrowserLaunchFailureIsSanitizedAndDoesNotSaveCredential()
    {
        var store = new MemoryStore();
        using var session = new DesktopAuthSession(Config, new Adapter(), store, new Clock(),
            _ => throw new InvalidOperationException("private-url-and-code"));
        Assert.Equal(DesktopAuthStatus.Unavailable, await session.SignInAsync());
        Assert.Null(store.Value);
    }

    private static DesktopAuthSession Session(MemoryStore store, Adapter adapter) =>
        new(Config, adapter, store, new Clock(), _ => throw new Xunit.Sdk.XunitException("Unexpected browser launch"));

    [Fact]
    public async Task TwoProductsShareRestoreConcurrentRefreshPendingRevocationAndLogout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-shared-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var toolkitConfig = new AuthConfiguration(Config.Issuer, Config.ClientId, DesktopAuthProduct.Toolkit);
            var benchmarkStore = new CredentialStore(Config, directory);
            var toolkitStore = new CredentialStore(toolkitConfig, directory);
            var adapter = new Adapter();
            Action<Uri> noBrowser = _ => throw new Xunit.Sdk.XunitException("Unexpected browser launch");
            using var benchmark = new DesktopAuthSession(Config, adapter, benchmarkStore, new Clock(), noBrowser);
            using var toolkit = new DesktopAuthSession(toolkitConfig, adapter, toolkitStore, new Clock(), noBrowser);
            benchmarkStore.Save(Credential(expired: true));
            var states = await Task.WhenAll(benchmark.RestoreAsync(), toolkit.RestoreAsync());
            Assert.All(states, state => Assert.Equal(DesktopAuthStatus.SignedIn, state));
            Assert.Equal(1, adapter.Refreshes);
            // A stale Sign in button in the other app must reuse the saved grant.
            Assert.Equal(DesktopAuthStatus.SignedIn, await toolkit.SignInAsync());
            adapter.RevokeError = new HttpRequestException("fixture-outage");
            Assert.Equal(DesktopAuthStatus.RevocationPending, await toolkit.SignOutAsync());
            Assert.Equal(DesktopAuthStatus.RevocationPending, await benchmark.RestoreAsync());
            Assert.Equal(DesktopAuthStatus.RevocationPending, await benchmark.SignInAsync());
            adapter.RevokeError = null;
            Assert.Equal(DesktopAuthStatus.SignedOut, await benchmark.SignOutAsync());
            Assert.Equal(DesktopAuthStatus.SignedOut, await toolkit.RestoreAsync());
            // No fallback to old per-product files can resurrect the signed-out session.
            Assert.Null(benchmarkStore.Load());
            Assert.Null(toolkitStore.Load());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task WaitingForOtherAppLeaseCanBeCanceledAndDoesNotOpenBrowser()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-shared-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CredentialStore(Config, directory);
            using var lease = store.AcquireLease();
            using var session = new DesktopAuthSession(Config, new Adapter(), store, new Clock(),
                _ => throw new Xunit.Sdk.XunitException("Unexpected browser launch"));
            using var cancellation = new CancellationTokenSource();
            var waiting = session.SignInAsync(cancellation.Token);
            Assert.False(waiting.IsCompleted);
            cancellation.Cancel();
            Assert.Equal(DesktopAuthStatus.Canceled, await waiting);
            Assert.Null(store.Load());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ConcurrentProductLoginsOpenOnlyOneBrowserAndBothRestoreAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-shared-auth-test-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var toolkitConfig = new AuthConfiguration(Config.Issuer, Config.ClientId, DesktopAuthProduct.Toolkit);
            var adapter = new Adapter();
            var browserOpened = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var browserCount = 0;
            void OpenBrowser(Uri uri) { Interlocked.Increment(ref browserCount); browserOpened.TrySetResult(uri); }
            using (var benchmark = new DesktopAuthSession(Config, adapter, new CredentialStore(Config, directory), new Clock(), OpenBrowser))
            using (var toolkit = new DesktopAuthSession(toolkitConfig, adapter, new CredentialStore(toolkitConfig, directory), new Clock(), OpenBrowser))
            {
                var first = benchmark.SignInAsync(cancellation.Token);
                var uri = await browserOpened.Task.WaitAsync(cancellation.Token);
                var second = toolkit.SignInAsync(cancellation.Token);
                Assert.False(second.IsCompleted);
                var query = QueryHelpers.ParseQuery(uri.Query);
                using var http = new HttpClient();
                await http.GetStringAsync(query["redirect_uri"] + "?code=fixture-code&state=" + query["state"], cancellation.Token);
                var statuses = await Task.WhenAll(first, second);
                Assert.All(statuses, status => Assert.Equal(DesktopAuthStatus.SignedIn, status));
                Assert.Equal(1, browserCount);
            }
            foreach (var configuration in new[] { Config, toolkitConfig })
            {
                using var restarted = new DesktopAuthSession(configuration, adapter, new CredentialStore(configuration, directory), new Clock(), OpenBrowser);
                Assert.Equal(DesktopAuthStatus.SignedIn, await restarted.RestoreAsync(cancellation.Token));
            }
            Assert.Equal(1, browserCount);
            Assert.Single(Directory.GetFiles(directory, "*.credential"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static DesktopCredential Credential(bool expired = false) => new()
    {
        AccessToken = "fixture-access", RefreshToken = "fixture-refresh",
        ExpiresAt = expired ? Now.AddSeconds(-1) : Now.AddHours(1)
    };

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MemoryStore : ICredentialStore
    {
        public DesktopCredential? Value;
        public bool FailSave;
        public IDisposable AcquireLease() => new MemoryStream();
        public DesktopCredential? Load() => Value;
        public void Clear() => Value = null;
        public void Save(DesktopCredential credential)
        {
            if (FailSave) throw new IOException("private-storage-path");
            Value = credential;
        }
    }

    private sealed class Adapter : IDesktopAuthAdapter
    {
        public Exception? RefreshError;
        public Exception? RevokeError;
        public int Refreshes;
        public DesktopCredential? Revoked;
        public string? Code;
        public string? Verifier;
        public Uri Authorize(PkceAttempt attempt, Uri redirect) => new(
            Config.Issuer + "/oauth/authorize?redirect_uri=" + Uri.EscapeDataString(redirect.AbsoluteUri) + "&state=" + attempt.State);
        public Task<DesktopCredential> ExchangeAsync(string code, string verifier, Uri redirect, CancellationToken cancellation)
        {
            Code = code;
            Verifier = verifier;
            return Task.FromResult(Credential());
        }
        public async Task<DesktopCredential> RefreshAsync(DesktopCredential credential, CancellationToken cancellation)
        {
            Refreshes++;
            await Task.Yield();
            if (RefreshError is not null) throw RefreshError;
            return new() { AccessToken = "fixture-new-access", RefreshToken = "fixture-new-refresh", ExpiresAt = Now.AddHours(1) };
        }
        public Task RevokeAsync(DesktopCredential credential, CancellationToken cancellation)
        {
            if (RevokeError is not null) throw RevokeError;
            Revoked = credential;
            return Task.CompletedTask;
        }
    }
}
