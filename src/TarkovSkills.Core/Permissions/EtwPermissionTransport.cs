using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace TarkovSkills.Core.Permissions;

internal static class EtwPermissionTransport
{
    internal static Task<EtwPermissionResult> RequestAsync(EtwPermissionAction action, CancellationToken cancellation) =>
        EtwPermissionWorkflow.RunAsync(action, EtwPermissionNative.CheckCurrent, LaunchAsync, cancellation);

    private static async Task<EtwPermissionResult> LaunchAsync(EtwPermissionAction action,
        EtwPermissionProtocol.Progress progress, CancellationToken cancellation)
    {
        var connected = await ConnectAsync(action, false, progress, cancellation).ConfigureAwait(false);
        connected.Pipe.Dispose();
        return connected.Result;
    }

    internal static async Task<CaptureAccessLaunch> LaunchCaptureAsync(CancellationToken cancellation)
    {
        IEtwCaptureConnection? connection = null;
        var result = await EtwPermissionWorkflow.RunAsync(EtwPermissionAction.Add,
            // An existing group membership may still be absent from this sign-in.
            // Real ordinary capture has already failed; do not skip the temporary helper.
            () => new(false, false, false), async (action, progress, token) =>
            {
                var connected = await ConnectAsync(action, true, progress, token).ConfigureAwait(false);
                if (connected.Result.Outcome is EtwPermissionOutcome.Added or EtwPermissionOutcome.AlreadyMember)
                    connection = new EtwCaptureConnection(connected.Pipe);
                else connected.Pipe.Dispose();
                return connected.Result;
            }, cancellation).ConfigureAwait(false);
        if (cancellation.IsCancellationRequested) { connection?.Dispose(); connection = null; }
        return new(result, connection);
    }

    private static async Task<(NamedPipeServerStream Pipe, EtwPermissionResult Result)> ConnectAsync(
        EtwPermissionAction action, bool captureSession, EtwPermissionProtocol.Progress progress, CancellationToken cancellation)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is null) throw new InvalidOperationException("Host identity is unavailable.");
        var request = new EtwPermissionRequest(action, EtwPermissionRequest.PipePrefix + Guid.NewGuid().ToString("N"), captureSession);
        var server = CreateServer(request.PipeName, identity.User);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Host executable is unavailable.");
            var launch = BuildLaunch(EtwPermissionNative.ApplicationId(), executable, request);
            // Shell elevation can wait for UAC. Keep the normal GUI responsive.
            await Task.Run(() => { using var child = Process.Start(launch); }, CancellationToken.None).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await server.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
            var result = await EtwPermissionProtocol.ParentAsync(server, action, progress, timeout.Token,
                () => VerifyHelper(server)).ConfigureAwait(false);
            if (captureSession && result.Outcome is EtwPermissionOutcome.Added or EtwPermissionOutcome.AlreadyMember)
            {
                var ready = await EtwCaptureWire.ReadAsync(server, timeout.Token).ConfigureAwait(false);
                if (ready != new CapturePacket(CapturePacketKind.Ready))
                    throw new InvalidDataException("The temporary collector did not become ready.");
            }
            return (server, result);
        }
        catch { server.Dispose(); throw; }
    }

    internal static ProcessStartInfo BuildLaunch(string? applicationId, string executable, EtwPermissionRequest request)
    {
        var launch = new ProcessStartInfo(EtwPermissionNative.LaunchTarget(applicationId, executable))
        {
            UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(executable) ?? ""
        };
        foreach (var argument in request.Arguments) launch.ArgumentList.Add(argument);
        return launch;
    }

    internal static NamedPipeServerStream CreateServer(string name, SecurityIdentifier owner)
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(owner);
        security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.ReadPermissions, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 256, 256, security);
    }

    internal static async Task<int> RunHelperAsync(string[] args)
    {
        if (!EtwPermissionRequest.TryParse(args, out var request)) return (int)EtwPermissionOutcome.InvalidRequest;
        if (!EtwPermissionNative.IsElevated()) return (int)EtwPermissionOutcome.NotElevated;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client = new NamedPipeClientStream(".", request!.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var originalUser = VerifyRequester(client);
            var result = await EtwPermissionProtocol.HelperAsync(client, request.Action,
                action => EtwPermissionNative.ChangeForUser(action, originalUser), timeout.Token).ConfigureAwait(false);
            if (request.CaptureSession && result.Outcome is EtwPermissionOutcome.Added or EtwPermissionOutcome.AlreadyMember)
            {
                using var job = new CaptureProcessJob();
                var runner = new PresentMonRunner(job.Add);
                await new CapturePacketWriter(client).WriteAsync(new(CapturePacketKind.Ready), timeout.Token).ConfigureAwait(false);
                await EtwCaptureBroker.RunAsync(client, (duration, started, diagnostic, cancellation) =>
                    runner.CaptureWindowAsync(duration, started, cancellation, diagnostic), runner.ProbeAsync).ConfigureAwait(false);
            }
            return (int)result.Outcome;
        }
        catch (Exception) { return (int)EtwPermissionOutcome.Failed; }
    }

    internal static SecurityIdentifier VerifyRequester(NamedPipeClientStream client)
    {
        if (!GetNamedPipeServerProcessId(client.SafePipeHandle, out var server))
            throw new InvalidOperationException("Setup requester is unavailable.");
        EtwPermissionNative.VerifyPeerImage(server);
        // Windows restricts which owner SID an unprivileged process can assign.
        // The server explicitly sets its token user as owner; a group owner is
        // rejected. Never inspect another account's process token or accept an
        // account from command-line arguments.
        var owner = client.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier
            ?? throw new InvalidOperationException("Setup requester account is unavailable.");
        if (!EtwPermissionNative.IsUser(owner)) throw new InvalidOperationException("Setup requester account is unavailable.");
        return owner;
    }

    internal static void VerifyHelper(NamedPipeServerStream server)
    {
        var elevated = false;
        server.RunAsClient(() => elevated = EtwPermissionNative.IdentificationClientIsElevated());
        if (!elevated) throw new InvalidOperationException("Setup helper is not elevated.");
    }

    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
}
