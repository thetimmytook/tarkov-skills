using System.ComponentModel;

namespace TarkovSkills.Core.Permissions;

public enum EtwPermissionAction : byte { Add = 1, Remove = 2 }

public enum EtwPermissionOutcome : byte
{
    Added = 1, AlreadyMember, Removed, AlreadyAbsent, Cancelled,
    AccessDenied, NotElevated, InvalidRequest, Failed, Unconfirmed, Available, TimedOut
}

public sealed record EtwPermissionResult(EtwPermissionOutcome Outcome, int? WindowsError = null)
{
    public string Message => Outcome switch
    {
        EtwPermissionOutcome.Added => "Windows capture access was added for your account. Ordinary access takes effect at your next Windows sign-in. An active temporary collector can capture without signing out.",
        EtwPermissionOutcome.AlreadyMember => "Your account is already a direct member of Performance Log Users. Ordinary access takes effect at your next Windows sign-in. An active temporary collector can capture without signing out.",
        EtwPermissionOutcome.Removed => "Your account's direct membership was removed. Sign out of Windows and sign back in for the change to take effect. Access through another group is unchanged.",
        EtwPermissionOutcome.AlreadyAbsent => "Your account has no direct membership to remove. Access through another group is unchanged.",
        EtwPermissionOutcome.Available => "This Windows sign-in already has performance tracing permission. No account membership was changed.",
        EtwPermissionOutcome.Cancelled => "Permission setup was cancelled before applying the change.",
        EtwPermissionOutcome.AccessDenied => "Windows denied the permission change. An administrator may need to configure access for this account.",
        EtwPermissionOutcome.NotElevated => "Permission setup did not receive administrator rights. No permission change was applied.",
        EtwPermissionOutcome.InvalidRequest => "The permission setup request could not be verified. No permission change was applied.",
        EtwPermissionOutcome.Unconfirmed => "The permission setup result could not be confirmed. Check Windows capture access before trying again.",
        EtwPermissionOutcome.TimedOut => "The permission helper did not become ready in time. No change was authorized. You can try again.",
        _ => "Windows capture access could not be configured. Check access before trying again."
    };

    internal static EtwPermissionResult FromNative(EtwPermissionAction action, int code) => (action, code) switch
    {
        (EtwPermissionAction.Add, 0) => new(EtwPermissionOutcome.Added),
        (EtwPermissionAction.Add, 1378) => new(EtwPermissionOutcome.AlreadyMember),
        (EtwPermissionAction.Remove, 0) => new(EtwPermissionOutcome.Removed),
        (EtwPermissionAction.Remove, 1377) => new(EtwPermissionOutcome.AlreadyAbsent),
        (_, 5) => new(EtwPermissionOutcome.AccessDenied, code),
        _ => new(EtwPermissionOutcome.Failed, code)
    };
}

public sealed record EtwPermissionState(bool CurrentTokenHasAccess, bool? DirectMembership, bool Elevated)
{
    public string Message => CurrentTokenHasAccess
        ? "This Windows sign-in has performance tracing permission. A successful capture is still required to verify access to the trace providers."
        : DirectMembership == true
            ? "Your account is configured, but this Windows sign-in has not picked up the permission yet. Enable a temporary collector to capture now. Ordinary access takes effect at your next Windows sign-in."
            : DirectMembership == false
                ? "This Windows sign-in does not have performance tracing permission. You can request a one-time administrator setup for your account."
                : "Windows capture permission could not be fully checked. No account settings were changed.";
}

public static class EtwPermissions
{
    public const string GroupSid = "S-1-5-32-559";
    public const string HelperArgument = "--setup-permissions";
    public const string CaptureHelperArgument = "--capture-helper";

    public static EtwPermissionState CheckCurrent() => EtwPermissionNative.CheckCurrent();

    public static Task<EtwPermissionResult> RequestChangeAsync(EtwPermissionAction action, CancellationToken cancellation = default) =>
        EtwPermissionTransport.RequestAsync(action, cancellation);

    public static bool IsHelperInvocation(IReadOnlyList<string> args) =>
        args.Any(value => value.Equals(HelperArgument, StringComparison.OrdinalIgnoreCase)
            || value.Equals(CaptureHelperArgument, StringComparison.OrdinalIgnoreCase));

    // Called before either GUI host's normal startup, single-instance mutex,
    // authentication or history access. Headless CLI never calls this entry point.
    public static Task<int> RunHelperAsync(string[] args) => EtwPermissionTransport.RunHelperAsync(args);

    internal static EtwPermissionResult? BeforeLaunch(EtwPermissionAction action, EtwPermissionState state) => action switch
    {
        EtwPermissionAction.Add when state.DirectMembership == true => new(EtwPermissionOutcome.AlreadyMember),
        EtwPermissionAction.Add when state.CurrentTokenHasAccess => new(EtwPermissionOutcome.Available),
        EtwPermissionAction.Remove when state.DirectMembership == false => new(EtwPermissionOutcome.AlreadyAbsent),
        EtwPermissionAction.Add or EtwPermissionAction.Remove => null,
        _ => new(EtwPermissionOutcome.InvalidRequest)
    };
}

internal sealed record EtwPermissionRequest(EtwPermissionAction Action, string PipeName, bool CaptureSession = false)
{
    internal const string PipePrefix = "TarkovSkills.EtwSetup.";

    internal static bool TryParse(IReadOnlyList<string> args, out EtwPermissionRequest? request)
    {
        request = null;
        if (args.Count == 3 && args[0] == EtwPermissions.CaptureHelperArgument && args[1] == "--pipe"
            && Guid.TryParseExact(args[2], "N", out var captureNonce))
        {
            request = new(EtwPermissionAction.Add, PipePrefix + captureNonce.ToString("N"), true);
            return true;
        }
        if (args.Count != 4 || args[0] != EtwPermissions.HelperArgument || args[2] != "--pipe"
            || !Guid.TryParseExact(args[3], "N", out var nonce)) return false;
        var action = args[1] switch { "add" => EtwPermissionAction.Add, "remove" => EtwPermissionAction.Remove, _ => (EtwPermissionAction)0 };
        if (action == 0) return false;
        request = new(action, PipePrefix + nonce.ToString("N"));
        return true;
    }

    internal string[] Arguments => CaptureSession
        ? [EtwPermissions.CaptureHelperArgument, "--pipe", PipeName[PipePrefix.Length..]]
        : [EtwPermissions.HelperArgument, Action == EtwPermissionAction.Add ? "add" : "remove", "--pipe", PipeName[PipePrefix.Length..]];
}

internal static class EtwPermissionWorkflow
{
    internal static async Task<EtwPermissionResult> RunAsync(EtwPermissionAction action,
        Func<EtwPermissionState> check,
        Func<EtwPermissionAction, EtwPermissionProtocol.Progress, CancellationToken, Task<EtwPermissionResult>> launch,
        CancellationToken cancellation)
    {
        var progress = new EtwPermissionProtocol.Progress();
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var early = EtwPermissions.BeforeLaunch(action, check());
            return early ?? await launch(action, progress, cancellation).ConfigureAwait(false);
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223 && !progress.ApplyAuthorized)
        { return new(EtwPermissionOutcome.Cancelled); }
        catch (Win32Exception error) when (error.NativeErrorCode == 5 && !progress.ApplyAuthorized)
        { return new(EtwPermissionOutcome.AccessDenied, 5); }
        catch (Win32Exception error)
        { return new(progress.ApplyAuthorized ? EtwPermissionOutcome.Unconfirmed : EtwPermissionOutcome.Failed, error.NativeErrorCode); }
        catch (OperationCanceledException)
        { return new(progress.ApplyAuthorized ? EtwPermissionOutcome.Unconfirmed
            : cancellation.IsCancellationRequested ? EtwPermissionOutcome.Cancelled : EtwPermissionOutcome.TimedOut); }
        catch (Exception) { return new(progress.ApplyAuthorized ? EtwPermissionOutcome.Unconfirmed : EtwPermissionOutcome.Failed); }
    }
}
