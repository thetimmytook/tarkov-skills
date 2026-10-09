using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

internal static class WindowsResourceMemory
{
    public static double? InstalledRam() => GetPhysicallyInstalledSystemMemory(out var kb) && kb > 0 ? kb * 1024d : null;

    public static MemoryReading? ReadMemory()
    {
        var info = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!GetPerformanceInfo(ref info, info.Size)) return null;
        var page = (double)info.PageSize.ToUInt64();
        var total = info.PhysicalTotal.ToUInt64() * page;
        var available = info.PhysicalAvailable.ToUInt64() * page;
        var used = info.CommitTotal.ToUInt64() * page;
        var limit = info.CommitLimit.ToUInt64() * page;
        return page > 0 && total > 0 && available <= total && used <= limit ? new(total, available, used, limit) : null;
    }

    public static IReadOnlyList<PagefileReading>? ReadPagefiles()
    {
        var result = new List<PagefileReading>();
        var pageSize = Environment.SystemPageSize;
        PagefileCallback callback = (IntPtr context, ref PagefileInformation info, string name) =>
        {
            result.Add(new(NormalizePath(name), info.TotalSize.ToUInt64() * (double)pageSize,
                info.TotalInUse.ToUInt64() * (double)pageSize));
            return true;
        };
        var success = EnumPageFilesW(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return success ? result : null;
    }

    private static string NormalizePath(string name) =>
        (name.StartsWith(@"\??\", StringComparison.Ordinal) || name.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? name[4..] : name).ToUpperInvariant();

    [StructLayout(LayoutKind.Sequential)] private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [StructLayout(LayoutKind.Sequential)] private struct PagefileInformation
    {
        public uint Size, Reserved;
        public UIntPtr TotalSize, TotalInUse, PeakUsage;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool PagefileCallback(IntPtr context, ref PagefileInformation info, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalKb);
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPerformanceInfo(ref PerformanceInformation info, uint size);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumPageFilesW(PagefileCallback callback, IntPtr context);
}
