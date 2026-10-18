using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TarkovSkills.Core.Permissions;

// Identity, account names and executable paths stay private to this implementation.
// No game process is opened: token inspection is limited to our setup participants.
internal static class EtwPermissionNative
{
    private static readonly SecurityIdentifier PerformanceGroup = new(EtwPermissions.GroupSid);
    private const uint QueryLimitedInformation = 0x1000;

    internal static EtwPermissionState CheckCurrent()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var elevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
        bool? direct = null;
        try { if (identity.User is not null) direct = IsDirectMember(identity.User); }
        catch (Exception) { /* Current token and unknown membership remain distinguishable. */ }
        return new(elevated || principal.IsInRole(PerformanceGroup), direct, elevated);
    }

    internal static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static EtwPermissionResult ChangeForUser(EtwPermissionAction action, SecurityIdentifier user)
    {
        if (!IsElevated()) return new(EtwPermissionOutcome.NotElevated);
        if (action is not (EtwPermissionAction.Add or EtwPermissionAction.Remove) || !IsUser(user))
            return new(EtwPermissionOutcome.InvalidRequest);
        var sidBytes = new byte[user.BinaryLength];
        user.GetBinaryForm(sidBytes, 0);
        var pinned = GCHandle.Alloc(sidBytes, GCHandleType.Pinned);
        try
        {
            var member = new MemberInfo { Sid = pinned.AddrOfPinnedObject() };
            var code = action == EtwPermissionAction.Add
                ? NetLocalGroupAddMembers(null, GroupName(), 0, ref member, 1)
                : NetLocalGroupDelMembers(null, GroupName(), 0, ref member, 1);
            return EtwPermissionResult.FromNative(action, code);
        }
        finally { pinned.Free(); }
    }

    private static bool IsDirectMember(SecurityIdentifier user)
    {
        nuint resume = 0;
        do
        {
            var code = NetLocalGroupGetMembers(null, GroupName(), 0, out var buffer, -1, out var count, out _, ref resume);
            try
            {
                if (code is not (0 or 234)) throw new Win32Exception(code);
                for (var index = 0; index < count; index++)
                    if (new SecurityIdentifier(Marshal.ReadIntPtr(buffer, index * IntPtr.Size)).Equals(user)) return true;
                if (code == 0) return false;
            }
            finally { if (buffer != IntPtr.Zero) NetApiBufferFree(buffer); }
        } while (resume != 0);
        return false;
    }

    private static string GroupName()
    {
        var account = (NTAccount)PerformanceGroup.Translate(typeof(NTAccount));
        return account.Value[(account.Value.IndexOf('\\') + 1)..];
    }

    internal static bool IsUser(SecurityIdentifier sid)
    {
        uint nameLength = 0, domainLength = 0;
        var bytes = new byte[sid.BinaryLength]; sid.GetBinaryForm(bytes, 0);
        _ = LookupAccountSid(null, bytes, null, ref nameLength, null, ref domainLength, out _);
        if (Marshal.GetLastWin32Error() != 122) return false;
        var name = new StringBuilder((int)nameLength);
        var domain = new StringBuilder((int)domainLength);
        return LookupAccountSid(null, bytes, name, ref nameLength, domain, ref domainLength, out var kind) && kind == 1;
    }

    internal static void VerifyPeerImage(uint processId)
    {
        using var process = OpenProcess(QueryLimitedInformation, false, processId);
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var image = new StringBuilder(32768); var size = image.Capacity;
        if (!QueryFullProcessImageName(process, 0, image, ref size)
            || !string.Equals(image.ToString(), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Setup peer verification failed.");
    }

    internal static bool IdentificationClientIsElevated()
    {
        // Called synchronously inside NamedPipeServerStream.RunAsClient. Reading an
        // identification token works for another administrator without SeDebugPrivilege
        // and cannot execute operations with that administrator's identity.
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        return identity.ImpersonationLevel == TokenImpersonationLevel.Identification
            && GetTokenInformation(identity.AccessToken, 20, out var elevation, Marshal.SizeOf<TokenElevation>(), out _)
            && elevation.IsElevated != 0;
    }

    internal static string LaunchTarget(string? applicationId, string executable) =>
        string.IsNullOrEmpty(applicationId) ? executable : @"shell:AppsFolder\" + applicationId;

    internal static string? ApplicationId()
    {
        uint length = 0;
        var code = GetCurrentApplicationUserModelId(ref length, null);
        if (code is 15700 or 15703) return null;
        if (code != 122) throw new Win32Exception(code);
        var value = new StringBuilder((int)length);
        code = GetCurrentApplicationUserModelId(ref length, value);
        if (code != 0) throw new Win32Exception(code);
        return value.ToString();
    }

    [StructLayout(LayoutKind.Sequential)] private struct MemberInfo { internal IntPtr Sid; }
    [StructLayout(LayoutKind.Sequential)] private struct TokenElevation { internal uint IsElevated; }
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] private static extern int NetLocalGroupAddMembers(string? server, string group, int level, ref MemberInfo buffer, int entries);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] private static extern int NetLocalGroupDelMembers(string? server, string group, int level, ref MemberInfo buffer, int entries);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] private static extern int NetLocalGroupGetMembers(string? server, string group, int level, out IntPtr buffer, int maximumLength, out int entriesRead, out int totalEntries, ref nuint resume);
    [DllImport("netapi32.dll")] private static extern int NetApiBufferFree(IntPtr buffer);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupAccountSid(string? system, byte[] sid, StringBuilder? name, ref uint nameLength, StringBuilder? domain, ref uint domainLength, out int use);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder name, ref int size);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int information, out TokenElevation value, int length, out int returnedLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentApplicationUserModelId(ref uint length, StringBuilder? value);
}
