using System.Buffers.Binary;

namespace TarkovSkills.Core.Permissions;

// A fixed-size local protocol has no account/path strings or arbitrary commands.
// Both endpoints verify the peer before authorizing a change. The helper
// derives the target user from its verified server's kernel-controlled owner SID,
// which the server sets from its token user, never from this wire.
internal static class EtwPermissionProtocol
{
    private const byte Version = 1, Hello = 0x50, Ready = 0x40, Apply = 0x41;
    internal sealed class Progress { internal bool ApplyAuthorized { get; set; } }

    internal static async Task<EtwPermissionResult> ParentAsync(Stream stream, EtwPermissionAction action,
        Progress progress, CancellationToken cancellation, Action? verifyHelper = null)
    {
        // Read a client message before using identification-level pipe authentication.
        var hello = new byte[1];
        await stream.ReadExactlyAsync(hello, cancellation).ConfigureAwait(false);
        if (hello[0] != Hello) return new(EtwPermissionOutcome.InvalidRequest);
        verifyHelper?.Invoke();
        await stream.WriteAsync(new byte[] { Version, (byte)action }, cancellation).ConfigureAwait(false);
        var ready = new byte[1];
        await stream.ReadExactlyAsync(ready, cancellation).ConfigureAwait(false);
        if (ready[0] != Ready) return new(EtwPermissionOutcome.InvalidRequest);
        cancellation.ThrowIfCancellationRequested();
        // Any uncertainty after authorization must not be reported as a no-change cancellation.
        progress.ApplyAuthorized = true;
        await stream.WriteAsync(new byte[] { Apply }, cancellation).ConfigureAwait(false);
        var result = new byte[5];
        await stream.ReadExactlyAsync(result, cancellation).ConfigureAwait(false);
        if (!Enum.IsDefined((EtwPermissionOutcome)result[0])) return new(EtwPermissionOutcome.Unconfirmed);
        var error = BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(1));
        return new((EtwPermissionOutcome)result[0], error == 0 ? null : error);
    }

    internal static async Task<EtwPermissionResult> HelperAsync(Stream stream, EtwPermissionAction expectedAction,
        Func<EtwPermissionAction, EtwPermissionResult> apply, CancellationToken cancellation)
    {
        await stream.WriteAsync(new byte[] { Hello }, cancellation).ConfigureAwait(false);
        var header = new byte[2];
        await stream.ReadExactlyAsync(header, cancellation).ConfigureAwait(false);
        if (header[0] != Version || header[1] != (byte)expectedAction
            || expectedAction is not (EtwPermissionAction.Add or EtwPermissionAction.Remove))
        {
            await WriteResultAsync(stream, new(EtwPermissionOutcome.InvalidRequest), cancellation).ConfigureAwait(false);
            return new(EtwPermissionOutcome.InvalidRequest);
        }
        await stream.WriteAsync(new byte[] { Ready }, cancellation).ConfigureAwait(false);
        var go = new byte[1];
        await stream.ReadExactlyAsync(go, cancellation).ConfigureAwait(false);
        if (go[0] != Apply) return new(EtwPermissionOutcome.InvalidRequest);
        cancellation.ThrowIfCancellationRequested();
        var result = apply(expectedAction);
        await WriteResultAsync(stream, result, cancellation).ConfigureAwait(false);
        return result;
    }

    private static async Task WriteResultAsync(Stream stream, EtwPermissionResult result, CancellationToken cancellation)
    {
        var bytes = new byte[5]; bytes[0] = (byte)result.Outcome;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), result.WindowsError ?? 0);
        await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
    }
}
