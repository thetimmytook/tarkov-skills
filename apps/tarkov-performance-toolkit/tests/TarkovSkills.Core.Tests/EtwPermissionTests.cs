using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwPermissionTests
{
    [Theory]
    [InlineData(true, false, EtwPermissionOutcome.Available)]
    [InlineData(true, true, EtwPermissionOutcome.AlreadyMember)]
    [InlineData(false, true, EtwPermissionOutcome.AlreadyMember)]
    public async Task ExistingAccessAndPendingSignInNeverLaunchAnElevatedProcess(bool token, bool direct, EtwPermissionOutcome expected)
    {
        var launches = 0;
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add, () => new(token, direct, false),
            (_, _, _) => { launches++; return Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Failed)); }, default);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(0, launches);
        if (token && !direct) Assert.DoesNotContain("direct member", result.Message);
    }

    [Fact]
    public async Task RemovingAnAbsentDirectMembershipDoesNotTouchAccessThroughOtherGroups()
    {
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Remove, () => new(true, false, false),
            (_, _, _) => throw new InvalidOperationException("Must not launch"), default);
        Assert.Equal(EtwPermissionOutcome.AlreadyAbsent, result.Outcome);
        Assert.Contains("another group", result.Message);
    }

    [Fact]
    public async Task UacCancellationIsExplicitAndDoesNotExposeWindowsExceptionText()
    {
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add, () => new(false, false, false),
            (_, _, _) => throw new Win32Exception(1223, @"C:\Users\private-person\token-secret"), default);
        Assert.Equal(EtwPermissionOutcome.Cancelled, result.Outcome);
        Assert.DoesNotContain("private-person", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("token-secret", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotInspectWindowsOrLaunchSetup()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add,
            () => throw new InvalidOperationException("Must not inspect"),
            (_, _, _) => throw new InvalidOperationException("Must not launch"), cancellation.Token);
        Assert.Equal(EtwPermissionOutcome.Cancelled, result.Outcome);
    }

    [Theory]
    [InlineData(false, EtwPermissionOutcome.TimedOut)]
    [InlineData(true, EtwPermissionOutcome.Unconfirmed)]
    public async Task TimeoutAfterAuthorizationNeverClaimsNoChange(bool authorized, EtwPermissionOutcome expected)
    {
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add, () => new(false, false, false),
            (_, progress, _) => { progress.ApplyAuthorized = authorized; throw new OperationCanceledException(); }, default);
        Assert.Equal(expected, result.Outcome);
        if (authorized) Assert.DoesNotContain("No change", result.Message);
    }

    [Theory]
    [InlineData(false, EtwPermissionOutcome.AccessDenied)]
    [InlineData(true, EtwPermissionOutcome.Unconfirmed)]
    public async Task FailedElevatedLaunchAndLostResultAreDifferent(bool authorized, EtwPermissionOutcome expected)
    {
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add, () => new(false, false, false),
            (_, progress, _) => { progress.ApplyAuthorized = authorized; throw new Win32Exception(5); }, default);
        Assert.Equal(expected, result.Outcome);
    }

    [Theory]
    [InlineData(EtwPermissionAction.Add, 0, EtwPermissionOutcome.Added)]
    [InlineData(EtwPermissionAction.Add, 1378, EtwPermissionOutcome.AlreadyMember)]
    [InlineData(EtwPermissionAction.Remove, 0, EtwPermissionOutcome.Removed)]
    [InlineData(EtwPermissionAction.Remove, 1377, EtwPermissionOutcome.AlreadyAbsent)]
    [InlineData(EtwPermissionAction.Add, 5, EtwPermissionOutcome.AccessDenied)]
    [InlineData(EtwPermissionAction.Add, 2220, EtwPermissionOutcome.Failed)]
    public void NativeOutcomesAreIdempotentAndExplainTheNextWindowsSignIn(EtwPermissionAction action, int error, EtwPermissionOutcome outcome)
    {
        var result = EtwPermissionResult.FromNative(action, error);
        Assert.Equal(outcome, result.Outcome);
        if (outcome == EtwPermissionOutcome.Removed)
            Assert.Contains("Sign out of Windows", result.Message);
        if (outcome == EtwPermissionOutcome.Added)
        {
            Assert.Contains("without signing out", result.Message);
            Assert.Contains("next Windows sign-in", result.Message);
        }
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    public void HelperArgumentsRoundTripWithoutAnAccountGroupOrProcessIdentifier(string operation)
    {
        var arguments = new[] { "--setup-permissions", operation, "--pipe", Guid.NewGuid().ToString("N") };
        Assert.True(EtwPermissionRequest.TryParse(arguments, out var request));
        Assert.Equal(arguments, request!.Arguments);
        Assert.StartsWith("TarkovSkills.EtwSetup.", request.PipeName);
        Assert.DoesNotContain(EtwPermissions.GroupSid, string.Join(' ', request.Arguments));
        Assert.DoesNotContain("--user", string.Join(' ', request.Arguments));
    }

    [Theory]
    [InlineData("--setup-permissions add --pipe bad")]
    [InlineData("--setup-permissions add --pipe 12345678123412341234123456781234 --user other")]
    [InlineData("--setup-permissions add --pipe \\remote\\pipe\\anything")]
    [InlineData("--setup-permissions add --user S-1-1-0")]
    [InlineData("--setup-permissions execute --pipe 12345678123412341234123456781234")]
    [InlineData("--setup-permissions remove --group Administrators")]
    public void HelperRejectsArbitraryTargetsAndCommands(string command) =>
        Assert.False(EtwPermissionRequest.TryParse(command.Split(' '), out _));

    [Fact]
    public void PackagedLaunchUsesTheShellAndPortableLaunchUsesOnlyItsOwnExecutable()
    {
        const string executable = @"C:\Program Files\Package\Toolkit.exe";
        var request = new EtwPermissionRequest(EtwPermissionAction.Add, EtwPermissionRequest.PipePrefix + Guid.NewGuid().ToString("N"));
        var packaged = EtwPermissionTransport.BuildLaunch("Product_family!App", executable, request);
        Assert.Equal(@"shell:AppsFolder\Product_family!App", packaged.FileName);
        Assert.Equal("runas", packaged.Verb);
        Assert.True(packaged.UseShellExecute);
        Assert.Equal(request.Arguments, packaged.ArgumentList);
        var portable = EtwPermissionTransport.BuildLaunch(null, executable, request);
        Assert.Equal(executable, portable.FileName);
        Assert.DoesNotContain("powershell", portable.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cmd.exe", portable.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeReadOnlyPeerInspectionVerifiesTheProgramWithoutOpeningItsToken()
    {
        EtwPermissionNative.VerifyPeerImage((uint)Environment.ProcessId);
    }

    [Fact]
    public void StateAndResultReportsContainNoAccountOrDeviceIdentifiers()
    {
        var state = EtwPermissionNative.CheckCurrent();
        var json = JsonSerializer.Serialize(new { state, result = new EtwPermissionResult(EtwPermissionOutcome.Added) });
        foreach (var value in new[] { Environment.UserName, Environment.MachineName, @"C:\Users\", "S-1-5-21-" })
            Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--setup-permissions")]
    [InlineData("--capture-helper")]
    public async Task CliRejectsSetupWithoutOpeningGuiOrUac(string helperArgument)
    {
        using var output = new StringWriter();
        var exit = await new ToolkitCli(output).RunAsync([helperArgument, "add", "--pipe", Guid.NewGuid().ToString("N")]);
        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("S-1-5-", output.ToString());
    }
}
