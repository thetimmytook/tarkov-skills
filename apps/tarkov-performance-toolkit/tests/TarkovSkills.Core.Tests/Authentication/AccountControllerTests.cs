using TarkovBenchmark.Feature.Authentication;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task InvalidConfigurationCannotExposeItsContentsOrInitializeAuthentication()
    {
        var path = Path.Combine(Path.GetTempPath(), "auth-config-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{\"issuer\":\"https://fixture.clerk.accounts.dev\",\"clientId\":\"fixture-client\",\"client_secret\":\"private-fixture\"}");
            var controller = AccountController.Create(DesktopAuthProduct.Benchmark, path);
            Assert.Contains("could not be initialized", controller.Detail);
            Assert.DoesNotContain("private-fixture", controller.Title + controller.Detail);
            Assert.False(controller.CanSignIn);
            Assert.False(controller.CanSignOut);
            await controller.StopAsync();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MissingConfigurationDisablesAccountActionsWithoutStartingAuth()
    {
        var controller = AccountController.Create(DesktopAuthProduct.Toolkit,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"));
        await controller.RestoreAsync();
        Assert.Contains("not configured", controller.Detail);
        Assert.False(controller.CanSignIn);
        Assert.False(controller.CanSignOut);
        Assert.False(controller.CanRetry);
        await controller.StopAsync();
    }

    [Theory]
    [InlineData(DesktopAuthStatus.SignedOut, true, false, false)]
    [InlineData(DesktopAuthStatus.SignedIn, false, true, false)]
    [InlineData(DesktopAuthStatus.SignInRequired, true, false, false)]
    [InlineData(DesktopAuthStatus.RevocationPending, false, true, false)]
    [InlineData(DesktopAuthStatus.Unavailable, false, true, true)]
    [InlineData(DesktopAuthStatus.StorageUnavailable, false, true, true)]
    [InlineData(DesktopAuthStatus.Canceled, false, true, true)]
    [InlineData(DesktopAuthStatus.Busy, false, false, false)]
    public async Task RestoredStateOffersOnlyValidActions(DesktopAuthStatus status, bool signIn, bool signOut, bool retry)
    {
        var controller = new AccountController(new Session { Status = status });
        await controller.RestoreAsync();
        Assert.Equal(signIn, controller.CanSignIn);
        Assert.Equal(signOut, controller.CanSignOut);
        Assert.Equal(retry, controller.CanRetry);
        Assert.Equal(status == DesktopAuthStatus.SignedIn, controller.CanContinue);
        await controller.StopAsync();
        Assert.False(controller.CanContinue);
    }

    [Theory]
    [InlineData(DesktopAuthStatus.SignedOut, 1, true)]
    [InlineData(DesktopAuthStatus.SignInRequired, 1, true)]
    [InlineData(DesktopAuthStatus.SignedIn, 0, true)]
    [InlineData(DesktopAuthStatus.RevocationPending, 0, false)]
    [InlineData(DesktopAuthStatus.Unavailable, 0, false)]
    [InlineData(DesktopAuthStatus.StorageUnavailable, 0, false)]
    [InlineData(DesktopAuthStatus.Busy, 0, false)]
    public async Task SubmitRestoresBeforeOpeningBrowserAndBlocksOnErrors(DesktopAuthStatus status, int signIns, bool canContinue)
    {
        var session = new Session { Status = status };
        var controller = new AccountController(session);
        Assert.Equal(0, session.Restores); // Constructing presentation does not start auth.
        await controller.EnsureSignedInAsync();
        Assert.Equal(1, session.Restores);
        Assert.Equal(signIns, session.SignIns);
        Assert.Equal(canContinue, controller.CanContinue);
        await controller.StopAsync();
    }

    [Fact]
    public async Task ClosingSubmitDuringLoginCancelsAndNeverEnablesContinuation()
    {
        var session = new Session { WaitForCancel = true };
        var controller = new AccountController(session);
        var preparing = controller.EnsureSignedInAsync();
        Assert.False(controller.CanContinue);
        await controller.StopAsync();
        await preparing;
        Assert.True(session.CleanupCompleted);
        Assert.True(session.Disposed);
        Assert.False(controller.CanContinue);
    }

    [Fact]
    public async Task RecheckBeforeContinuationDetectsLogoutInOtherApp()
    {
        var session = new Session { Status = DesktopAuthStatus.SignedIn };
        var controller = new AccountController(session);
        await controller.EnsureSignedInAsync();
        Assert.True(controller.CanContinue);
        session.Status = DesktopAuthStatus.SignedOut;
        await controller.RestoreAsync();
        Assert.False(controller.CanContinue);
        Assert.Equal(0, session.SignIns);
        await controller.StopAsync();
    }

    [Fact]
    public async Task LoginPreventsDuplicateAttemptsAndAllowsCancelThenRetry()
    {
        var session = new Session { WaitForCancel = true };
        var controller = new AccountController(session);
        await controller.RestoreAsync();
        var signIn = controller.SignInAsync();
        Assert.True(controller.CanCancel);
        Assert.False(controller.CanSignIn);
        Assert.False(controller.CanSignOut);
        await controller.SignInAsync();
        controller.CancelSignIn();
        await signIn;
        Assert.Equal(1, session.SignIns);
        Assert.True(controller.CanSignIn);
        Assert.Contains("canceled", controller.Detail);
        await controller.StopAsync();
    }

    [Fact]
    public async Task CloseCancelsLoginAndAwaitsCleanupBeforeDisposal()
    {
        var session = new Session { WaitForCancel = true };
        var controller = new AccountController(session);
        await controller.RestoreAsync();
        var signIn = controller.SignInAsync();
        await controller.StopAsync();
        Assert.True(signIn.IsCompleted);
        Assert.True(session.CleanupCompleted);
        Assert.True(session.Disposed);
        await controller.StopAsync();
        await controller.SignInAsync();
        Assert.Equal(1, session.SignIns);
    }

    [Fact]
    public async Task TimerDiscoversOtherAppsLoginAndLogoutWithoutOpeningBrowserAndStopsOnOutage()
    {
        var session = new Session();
        var controller = new AccountController(session);
        await controller.RestoreAsync();
        await controller.RefreshIfNeededAsync();
        Assert.Equal(2, session.Restores);
        session.Status = DesktopAuthStatus.SignedIn;
        await controller.RefreshIfNeededAsync();
        Assert.False(controller.CanSignIn);
        Assert.True(controller.CanSignOut);
        session.Status = DesktopAuthStatus.SignedOut;
        await controller.RefreshIfNeededAsync();
        Assert.True(controller.CanSignIn);
        Assert.Equal(0, session.SignIns);
        session.Status = DesktopAuthStatus.Unavailable;
        await controller.RefreshIfNeededAsync();
        await controller.RefreshIfNeededAsync();
        Assert.Equal(5, session.Restores); // No automatic retry loop during outages.
        await controller.StopAsync();
    }

    [Fact]
    public async Task IncompleteSignOutOffersRetryAndNeverClaimsSuccess()
    {
        var session = new Session { Status = DesktopAuthStatus.SignedIn, RevokeFails = true };
        var controller = new AccountController(session);
        await controller.RestoreAsync();
        await controller.SignOutAsync();
        Assert.Contains("incomplete", controller.Title);
        Assert.False(controller.CanSignIn);
        Assert.True(controller.CanSignOut);
        session.RevokeFails = false;
        await controller.SignOutAsync();
        Assert.Equal("Account · Signed out", controller.Title);
        Assert.True(controller.CanSignIn);
        await controller.StopAsync();
    }

    [Fact]
    public async Task UnexpectedProviderExceptionNeverBecomesUiText()
    {
        var controller = new AccountController(new Session { Throw = true });
        await controller.RestoreAsync();
        Assert.DoesNotContain("private", controller.Title + controller.Detail);
        Assert.True(controller.CanRetry);
        await controller.StopAsync();
    }

    private sealed class Session : IDesktopAuthSession
    {
        public DesktopAuthStatus Status = DesktopAuthStatus.SignedOut;
        public int Restores;
        public int SignIns;
        public bool WaitForCancel;
        public bool CleanupCompleted;
        public bool Disposed;
        public bool RevokeFails;
        public bool Throw;

        public Task<DesktopAuthStatus> RestoreAsync(CancellationToken cancellation = default)
        {
            Restores++;
            if (Throw) throw new InvalidOperationException("private-token-and-path");
            return Task.FromResult(Status);
        }

        public async Task<DesktopAuthStatus> SignInAsync(CancellationToken cancellation = default)
        {
            SignIns++;
            if (WaitForCancel)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); }
                catch (OperationCanceledException)
                {
                    await Task.Yield();
                    CleanupCompleted = true;
                    return DesktopAuthStatus.Canceled;
                }
            }
            return Status = DesktopAuthStatus.SignedIn;
        }

        public Task<DesktopAuthStatus> SignOutAsync(CancellationToken cancellation = default) =>
            Task.FromResult(Status = RevokeFails ? DesktopAuthStatus.RevocationPending : DesktopAuthStatus.SignedOut);

        public void Dispose()
        {
            if (WaitForCancel && SignIns != 0) Assert.True(CleanupCompleted);
            Assert.False(Disposed);
            Disposed = true;
        }
    }
}
