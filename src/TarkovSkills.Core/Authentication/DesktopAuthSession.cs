using System.Diagnostics;

namespace TarkovSkills.Core.Authentication;

// Public results contain no tokens, provider error bodies or identity claims.
public enum DesktopAuthStatus
{
    SignedOut, SignedIn, SignInRequired, Unavailable, Canceled, RevocationPending, StorageUnavailable, Busy
}

public interface IDesktopAuthSession : IDisposable
{
    Task<DesktopAuthStatus> RestoreAsync(CancellationToken cancellation = default);
    Task<DesktopAuthStatus> SignInAsync(CancellationToken cancellation = default);
    Task<DesktopAuthStatus> SignOutAsync(CancellationToken cancellation = default);
}

public sealed class DesktopAuthSession : IDesktopAuthSession
{
    private readonly AuthConfiguration config;
    private readonly IDesktopAuthAdapter adapter;
    private readonly ICredentialStore store;
    private readonly TimeProvider clock;
    private readonly Action<Uri> openBrowser;
    private readonly HttpClient? ownedHttp;
    private readonly SemaphoreSlim gate = new(1, 1);

    public static DesktopAuthSession Create(AuthConfiguration configuration)
    {
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = 65536
        };
        try
        {
            return new(configuration, new ClerkDesktopAdapter(configuration, http, TimeProvider.System),
                new CredentialStore(configuration, CredentialStore.SharedDirectory),
                TimeProvider.System, uri =>
                {
                    using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }, http);
        }
        catch
        {
            http.Dispose();
            throw new AuthenticationDenied();
        }
    }

    internal DesktopAuthSession(AuthConfiguration config, IDesktopAuthAdapter adapter, ICredentialStore store,
        TimeProvider clock, Action<Uri> openBrowser, HttpClient? ownedHttp = null)
    {
        this.config = config;
        this.adapter = adapter;
        this.store = store;
        this.clock = clock;
        this.openBrowser = openBrowser;
        this.ownedHttp = ownedHttp;
    }

    // Used on startup and periodically by the shared UI. It never opens the browser.
    // A restored local grant is not a verified backend account or publication permission.
    public Task<DesktopAuthStatus> RestoreAsync(CancellationToken cancellation = default) =>
        RunAsync(() => RestoreCoreAsync(cancellation), cancellation);

    private async Task<DesktopAuthStatus> RestoreCoreAsync(CancellationToken cancellation)
    {
        var credential = store.Load();
        if (credential is null) return DesktopAuthStatus.SignedOut;
        if (credential.RevocationPending) return DesktopAuthStatus.RevocationPending;
        if (credential.ExpiresAt > clock.GetUtcNow().AddMinutes(1)) return DesktopAuthStatus.SignedIn;
        return await RefreshCoreAsync(credential, cancellation);
    }

    private async Task<DesktopAuthStatus> RefreshCoreAsync(DesktopCredential credential, CancellationToken cancellation)
    {
        try
        {
            var refreshed = await adapter.RefreshAsync(credential, cancellation);
            return await PersistAsync(refreshed);
        }
        catch (CredentialRejected)
        {
            store.Clear();
            return DesktopAuthStatus.SignInRequired;
        }
    }

    // Only the trusted API transport in Core receives a token. Keep the shared-store
    // lease across refresh and request so another product cannot rotate or sign out midway.
    // The callback returns true only for an explicit HTTP 401 response.
    internal Task<DesktopAuthStatus> AuthorizeRequestAsync(Func<string, Task<bool>> send,
        CancellationToken cancellation) => RunAsync(async () =>
    {
        var status = await RestoreCoreAsync(cancellation);
        if (status != DesktopAuthStatus.SignedIn) return status;
        var credential = store.Load()!;
        if (!await send(credential.AccessToken)) return DesktopAuthStatus.SignedIn;

        // Retry only an explicit 401, once. Network errors never replay a request.
        status = await RefreshCoreAsync(credential, cancellation);
        if (status != DesktopAuthStatus.SignedIn) return status;
        await send(store.Load()!.AccessToken);
        // A repeated API 401 may also mean a provider outage: retain the credential.
        // The API result still reports Unauthorized, never successful authentication.
        return DesktopAuthStatus.SignedIn;
    }, cancellation);

    public Task<DesktopAuthStatus> SignInAsync(CancellationToken cancellation = default) => RunAsync(async () =>
    {
        var previous = store.Load();
        if (previous is not null)
            return await RestoreCoreAsync(cancellation);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var attempt = new PkceAttempt();
        await using var callback = await LoopbackCallback.StartAsync(attempt, config.Issuer, timeout.Token);
        openBrowser(adapter.Authorize(attempt, callback.RedirectUri));
        var code = await callback.ReceiveAsync(timeout.Token);
        var credential = await adapter.ExchangeAsync(code, attempt.Verifier, callback.RedirectUri, timeout.Token);
        return await PersistAsync(credential);
    }, cancellation);

    public Task<DesktopAuthStatus> SignOutAsync(CancellationToken cancellation = default) => RunAsync(async () =>
    {
        var credential = store.Load();
        if (credential is null) return DesktopAuthStatus.SignedOut;
        // Persist intent before network I/O so restart cannot reactivate a half-revoked grant.
        credential.RevocationPending = true;
        store.Save(credential);
        try
        {
            await adapter.RevokeAsync(credential, cancellation);
        }
        catch
        {
            return DesktopAuthStatus.RevocationPending;
        }
        store.Clear();
        return DesktopAuthStatus.SignedOut;
    }, cancellation);

    private async Task<DesktopAuthStatus> PersistAsync(DesktopCredential credential)
    {
        try
        {
            store.Save(credential);
            return DesktopAuthStatus.SignedIn;
        }
        catch
        {
            // A rotated grant must not remain active just because local persistence failed.
            // Independent cancellation allows cleanup even after the user canceled login.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await adapter.RevokeAsync(credential, cleanup.Token); }
            catch { /* Remote cleanup cannot be guaranteed during an outage. */ }
            return DesktopAuthStatus.StorageUnavailable;
        }
    }

    private async Task<DesktopAuthStatus> RunAsync(Func<Task<DesktopAuthStatus>> operation, CancellationToken cancellation)
    {
        try
        {
            await gate.WaitAsync(cancellation);
            try
            {
                using var lease = await AcquireLeaseAsync(cancellation);
                return await operation();
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) { return DesktopAuthStatus.Canceled; }
        catch (CredentialStoreBusy) { return DesktopAuthStatus.Busy; }
        catch (IOException) { return DesktopAuthStatus.StorageUnavailable; }
        catch (UnauthorizedAccessException) { return DesktopAuthStatus.StorageUnavailable; }
        catch (System.Security.Cryptography.CryptographicException) { return DesktopAuthStatus.StorageUnavailable; }
        catch { return DesktopAuthStatus.Unavailable; }
    }

    private async Task<IDisposable> AcquireLeaseAsync(CancellationToken cancellation)
    {
        var wait = Stopwatch.StartNew();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            try { return store.AcquireLease(); }
            catch (CredentialStoreBusy) when (wait.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(100, cancellation);
            }
        }
    }

    // The host cancels and awaits pending operations before disposal.
    public void Dispose()
    {
        ownedHttp?.Dispose();
        gate.Dispose();
    }
}
