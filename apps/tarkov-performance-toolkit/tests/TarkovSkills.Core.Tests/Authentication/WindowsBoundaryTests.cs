using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class WindowsBoundaryTests
{
    private const string Issuer = "https://fixture.clerk.accounts.dev";

    [Fact]
    public async Task ActualLoopbackRejectsWrongStateDuplicatesAndTokenUrlsThenAcceptsOnlyOnce()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var attempt = new PkceAttempt();
        await using var callback = await LoopbackCallback.StartAsync(attempt, Issuer, timeout.Token);
        Assert.Equal("127.0.0.1", callback.RedirectUri.Host);
        Assert.NotEqual(80, callback.RedirectUri.Port);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var state = attempt.State;
        foreach (var query in new[] {
            "?code=private-code&state=wrong",
            $"?code=private-code&state={state}&state={state}",
            $"?code=private-code&state={state}&access_token=private-token",
            $"?code=private-code&state={state}&iss=https://wrong.example",
            $"?code=private-code&state={state}&error=access_denied",
            $"?code=&state={state}" })
        {
            using var rejected = await http.GetAsync(callback.RedirectUri + query, timeout.Token);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.DoesNotContain("private-", await rejected.Content.ReadAsStringAsync(timeout.Token));
        }

        var url = callback.RedirectUri + $"?code=private-code&state={state}";
        using var accepted = await http.GetAsync(url, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("no-store", accepted.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("private-code", await accepted.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal("private-code", await callback.ReceiveAsync(timeout.Token));
        using var replay = await http.GetAsync(url, timeout.Token);
        Assert.Equal(HttpStatusCode.Gone, replay.StatusCode);
    }

    [Fact]
    public async Task CallbackRejectsForeignHostAndPostRequests()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var attempt = new PkceAttempt();
        await using var callback = await LoopbackCallback.StartAsync(attempt, Issuer, timeout.Token);
        using var http = new HttpClient();
        var url = callback.RedirectUri + $"?code=code&state={attempt.State}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Host = "attacker.example";
        using var response = await http.SendAsync(request, timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var posted = await http.PostAsync(url, null, timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, posted.StatusCode);
    }

    [Fact]
    public async Task DenialIsSanitizedAndCancellationClosesListener()
    {
        var attempt = new PkceAttempt();
        Uri uri;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            await using var callback = await LoopbackCallback.StartAsync(attempt, Issuer, timeout.Token);
            uri = callback.RedirectUri;
            using var http = new HttpClient();
            using var denied = await http.GetAsync(uri + $"?state={attempt.State}&error=access_denied&error_description=private-detail", timeout.Token);
            Assert.DoesNotContain("private-detail", await denied.Content.ReadAsStringAsync(timeout.Token));
            await Assert.ThrowsAsync<AuthenticationDenied>(() => callback.ReceiveAsync(timeout.Token));
        }

        // A new listener can bind the exact port after disposal. This avoids HttpClient retries
        // turning connection refusal into a platform-dependent timeout exception.
        using var rebound = new TcpListener(IPAddress.Loopback, uri.Port);
        rebound.Start();
        rebound.Stop();
        using var canceled = new CancellationTokenSource();
        await using var waiting = await LoopbackCallback.StartAsync(new PkceAttempt(), Issuer, CancellationToken.None);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.ReceiveAsync(canceled.Token));
    }

    [Fact]
    public void RealDpapiRoundTripTamperDetectionAtomicReplacementAndExclusiveLease()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CredentialStore(new AuthConfiguration(Issuer, "client_fixture", DesktopAuthProduct.Benchmark), directory);
            using (store.AcquireLease())
            {
                Assert.Throws<CredentialStoreBusy>(() => store.AcquireLease());
                Assert.Null(store.Load());
                store.Save(Credential("fixture-private-access"));
                Assert.Equal("fixture-private-access", store.Load()!.AccessToken);
                var file = Directory.GetFiles(directory, "*.credential").Single();
                Assert.DoesNotContain("fixture-private-access", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
                store.Save(Credential("rotated-private-access"));
                Assert.Equal("rotated-private-access", store.Load()!.AccessToken);
                Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
                var damaged = File.ReadAllBytes(file);
                damaged[damaged.Length / 2] ^= 0xff;
                File.WriteAllBytes(file, damaged);
                Assert.Throws<CryptographicException>(() => store.Load());
                store.Clear();
                Assert.Null(store.Load());
            }

            using var released = store.AcquireLease();
        }
        finally
        {
            // Only the unique test directory created above; no product or live credential paths.
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://fixture.clerk.accounts.dev", "other_client")]
    [InlineData("https://other.clerk.accounts.dev", "client_fixture")]
    public void CredentialCannotBeLoadedUnderAnotherClientOrIssuerBinding(string issuer, string client)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = new CredentialStore(new AuthConfiguration(Issuer, "client_fixture", DesktopAuthProduct.Benchmark), directory);
            var other = new CredentialStore(new AuthConfiguration(issuer, client, DesktopAuthProduct.Toolkit), directory);
            original.Save(Credential("private-fixture"));
            Assert.Null(other.Load());
            // A copied credential cannot cross environment/client boundaries.
            var originalFile = Directory.GetFiles(directory, "*.credential").Single();
            other.Save(Credential("other-fixture"));
            var otherFile = Directory.GetFiles(directory, "*.credential").Single(path => path != originalFile);
            File.Copy(originalFile, otherFile, overwrite: true);
            Assert.Throws<CryptographicException>(() => other.Load());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SharedOAuthClientSharesEncryptedCredentialLeaseRotationAndLogout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tarkov-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var benchmark = new CredentialStore(new AuthConfiguration(Issuer, "shared_client", DesktopAuthProduct.Benchmark), directory);
            var toolkit = new CredentialStore(new AuthConfiguration(Issuer, "shared_client", DesktopAuthProduct.Toolkit), directory);
            using var benchmarkLease = benchmark.AcquireLease();
            Assert.Throws<CredentialStoreBusy>(() => toolkit.AcquireLease());
            benchmark.Save(Credential("fixture-benchmark-access"));
            Assert.Equal("fixture-benchmark-access", toolkit.Load()!.AccessToken);
            toolkit.Save(Credential("fixture-toolkit-access"));
            Assert.Equal("fixture-toolkit-access", benchmark.Load()!.AccessToken);
            Assert.Equal("fixture-toolkit-access", toolkit.Load()!.AccessToken);
            benchmark.Clear();
            Assert.Null(benchmark.Load());
            Assert.Null(toolkit.Load());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static DesktopCredential Credential(string access) => new()
    {
        AccessToken = access,
        RefreshToken = "fixture-private-refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };
}
