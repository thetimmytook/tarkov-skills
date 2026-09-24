using System.ComponentModel;
using System.IO;
using TarkovSkills.Core.Authentication;

namespace TarkovBenchmark.Feature.Authentication;

// Owns only account presentation/lifetime. It cannot read or submit benchmark runs.
internal sealed class AccountController : INotifyPropertyChanged
{
    private readonly IDesktopAuthSession? session;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private Task active = Task.CompletedTask;
    private Task? shutdown;
    private DesktopAuthStatus status = DesktopAuthStatus.Unavailable;
    private bool stopped;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Title { get; private set; } = "Account · Checking sign-in";
    public string Detail { get; private set; } = "Signing in does not upload your benchmarks.";
    public bool IsBusy { get; private set; }
    public bool CanContinue => !stopped && !IsBusy && status == DesktopAuthStatus.SignedIn;
    public bool CanSignIn => !stopped && !IsBusy && session is not null &&
        status is DesktopAuthStatus.SignedOut or DesktopAuthStatus.SignInRequired or DesktopAuthStatus.Canceled;
    public bool CanSignOut => !stopped && !IsBusy && session is not null &&
        status is DesktopAuthStatus.SignedIn or DesktopAuthStatus.RevocationPending or DesktopAuthStatus.Unavailable or DesktopAuthStatus.StorageUnavailable;
    public bool CanRetry => !stopped && !IsBusy && session is not null &&
        status is DesktopAuthStatus.Unavailable or DesktopAuthStatus.StorageUnavailable;
    public bool CanCancel { get; private set; }

    internal AccountController(IDesktopAuthSession? session) => this.session = session;

    public static AccountController Create(DesktopAuthProduct product, string configurationPath)
    {
        if (!File.Exists(configurationPath)) return Unconfigured("Sign-in is not configured for this build. Local tools remain available.");
        try
        {
            return new(DesktopAuthSession.Create(AuthConfiguration.Read(configurationPath, product)));
        }
        catch
        {
            // Never show JSON/parser, filesystem or provider exception text in the UI.
            return Unconfigured("Sign-in could not be initialized. Local tools remain available. Restart the app to try again.");
        }
    }

    private static AccountController Unconfigured(string detail) => new(null)
    {
        Title = "Account · Sign-in unavailable", Detail = detail
    };

    public Task RestoreAsync() => StartAsync("Account · Checking sign-in", false,
        token => session!.RestoreAsync(token));

    // Only an explicit Submit opens this flow. Restore and polling never launch a browser.
    public async Task EnsureSignedInAsync()
    {
        await RestoreAsync();
        if (CanSignIn) await SignInAsync();
    }

    public Task RefreshIfNeededAsync() => status is DesktopAuthStatus.Unavailable or DesktopAuthStatus.StorageUnavailable
        ? Task.CompletedTask : RestoreAsync();

    public Task SignInAsync() => CanSignIn
        ? StartAsync("Account · Waiting for your browser", true, token => session!.SignInAsync(token))
        : Task.CompletedTask;

    public Task SignOutAsync() => CanSignOut
        ? StartAsync("Account · Signing out", false, token => session!.SignOutAsync(token))
        : Task.CompletedTask;

    public void CancelSignIn()
    {
        if (CanCancel) operation?.Cancel();
    }

    private Task StartAsync(string title, bool canCancel, Func<CancellationToken, Task<DesktopAuthStatus>> action)
    {
        if (stopped || session is null || IsBusy) return active;
        IsBusy = true;
        CanCancel = canCancel;
        Title = title;
        if (canCancel) Detail = "Complete sign-in in your system browser, then return here.";
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        Notify();
        active = CompleteAsync(action, operation, canCancel);
        return active;
    }

    private async Task CompleteAsync(Func<CancellationToken, Task<DesktopAuthStatus>> action, CancellationTokenSource cancellation, bool signingIn)
    {
        try
        {
            var result = await action(cancellation.Token);
            // A refresh HTTP timeout must not expose Sign in while a saved grant still exists.
            if (result == DesktopAuthStatus.Canceled && !signingIn) result = DesktopAuthStatus.Unavailable;
            if (!stopped) Apply(result);
        }
        catch
        {
            if (!stopped) Apply(DesktopAuthStatus.Unavailable);
        }
        finally
        {
            cancellation.Dispose();
            operation = null;
            IsBusy = false;
            CanCancel = false;
            if (!stopped) Notify();
        }
    }

    private void Apply(DesktopAuthStatus result)
    {
        status = result;
        (Title, Detail) = result switch
        {
            DesktopAuthStatus.SignedIn => ("Account · Signed in", "Shared with Benchmark and Toolkit. Sign out signs out both apps."),
            DesktopAuthStatus.SignedOut => ("Account · Signed out", "Sign in is optional. Local tools work without an account."),
            DesktopAuthStatus.SignInRequired => ("Account · Sign in again", "Your saved sign-in is no longer valid. Please sign in again."),
            DesktopAuthStatus.Canceled => ("Account · Signed out", "Sign-in was canceled or timed out. You can try again."),
            DesktopAuthStatus.RevocationPending => ("Account · Sign-out incomplete", "Access is disabled for both apps. Check your connection and choose Sign out again to finish revocation."),
            DesktopAuthStatus.Busy => ("Account · Another app is signing in or out", "Complete the account action in Benchmark or Toolkit. This app will check again automatically."),
            DesktopAuthStatus.StorageUnavailable => ("Account · Secure storage unavailable", "Sign-in could not be saved or read. Remote sign-out may be unconfirmed. Retry, or choose Sign out."),
            _ => ("Account · Sign-in unavailable", "Could not check or complete sign-in. Check your connection and retry, or choose Sign out.")
        };
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public Task StopAsync() => shutdown ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        stopped = true;
        lifetime.Cancel();
        await active;
        session?.Dispose();
        lifetime.Dispose();
    }
}
