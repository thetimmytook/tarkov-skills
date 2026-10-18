using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwPermissionPipeTests
{
    [Fact]
    public void SetupPipeAclGrantsOnlyTheOwnerAdministratorsAndSystem()
    {
        using var identity = WindowsIdentity.GetCurrent();
        Assert.NotNull(identity.User);
        using var server = EtwPermissionTransport.CreateServer(EtwPermissionRequest.PipePrefix + Guid.NewGuid().ToString("N"), identity.User);
        var acl = server.GetAccessControl();
        Assert.True(acl.AreAccessRulesProtected);
        Assert.True(Assert.IsType<SecurityIdentifier>(acl.GetOwner(typeof(SecurityIdentifier))).Equals(identity.User));
        var rules = acl.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToArray();
        Assert.Equal(3, rules.Length);
        Assert.All(rules, rule => Assert.Equal(AccessControlType.Allow, rule.AccessControlType));
        foreach (var broad in new[] { WellKnownSidType.WorldSid, WellKnownSidType.AuthenticatedUserSid, WellKnownSidType.InteractiveSid })
            Assert.DoesNotContain(rules, rule => ((SecurityIdentifier)rule.IdentityReference).IsWellKnown(broad));
    }

    [Fact]
    public async Task RealLocalPipeBindsTheRequesterAndCarriesOnlyTheAuthorizedAction()
    {
        using var identity = WindowsIdentity.GetCurrent();
        Assert.NotNull(identity.User);
        var name = EtwPermissionRequest.PipePrefix + Guid.NewGuid().ToString("N");
        using var server = EtwPermissionTransport.CreateServer(name, identity.User);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connected = server.WaitForConnectionAsync(timeout.Token);
        await client.ConnectAsync(timeout.Token);
        await connected;
        var requester = EtwPermissionTransport.VerifyRequester(client);
        Assert.True(requester.Equals(identity.User));
        var changes = 0;
        // No real membership call: this tests the native pipe and fixed protocol only.
        var helper = EtwPermissionProtocol.HelperAsync(client, EtwPermissionAction.Add,
            _ => { changes++; return new(EtwPermissionOutcome.Added); }, timeout.Token);
        var parent = EtwPermissionProtocol.ParentAsync(server, EtwPermissionAction.Add, new(), timeout.Token, () =>
        {
            if (!EtwPermissionNative.IsElevated()) Assert.Throws<InvalidOperationException>(() => EtwPermissionTransport.VerifyHelper(server));
            else EtwPermissionTransport.VerifyHelper(server);
        });
        await Task.WhenAll(helper, parent);
        Assert.Equal(EtwPermissionOutcome.Added, parent.Result.Outcome);
        Assert.Equal(1, changes);
    }
}
