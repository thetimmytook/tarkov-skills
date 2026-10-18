using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TarkovSkills.Core.Permissions;

// Own only our collector/cleanup children. Closing/crashing the helper closes
// this job handle and Windows terminates its members, including PresentMon.
internal sealed class CaptureProcessJob : IDisposable
{
    private readonly SafeFileHandle _job;
    internal CaptureProcessJob()
    {
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(_job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()))
        { var error = Marshal.GetLastWin32Error(); _job.Dispose(); throw new Win32Exception(error); }
    }

    internal void Add(Process process)
    {
        if (!AssignProcessToJobObject(_job, process.SafeHandle))
        {
            var error = Marshal.GetLastWin32Error();
            if (process.HasExited) return;
            try { process.Kill(true); } catch { }
            throw new Win32Exception(error);
        }
    }

    public void Dispose() => _job.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        internal long ProcessTime, JobTime;
        internal uint Flags;
        internal UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        internal uint ActiveProcesses;
        internal UIntPtr Affinity;
        internal uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        internal BasicLimits Basic;
        internal IoCounters Io;
        internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int information, ref ExtendedLimits limits, int length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);
}
