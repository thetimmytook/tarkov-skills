using System.Runtime.InteropServices;

namespace TarkovSkills.Core;

// Native PDH avoids localized counter paths and introduces no executable dependency.
internal sealed class WindowsPerformanceCounters : IDisposable
{
    private IntPtr query;
    private readonly Dictionary<string, CounterBuffer> counters = [];
    private sealed class CounterBuffer(IntPtr handle)
    {
        public IntPtr Handle { get; } = handle;
        public IntPtr Buffer;
        public uint Size;
    }

    public WindowsPerformanceCounters()
    {
        if (PdhOpenQueryW(null, UIntPtr.Zero, out query) != 0) { query = IntPtr.Zero; return; }
        Add("cpu", @"\Processor Information(*)\% Processor Time");
        Add("graphics", @"\GPU Engine(*)\Utilization Percentage");
        Add("dedicated", @"\GPU Adapter Memory(*)\Dedicated Usage");
        Add("shared", @"\GPU Adapter Memory(*)\Shared Usage");
    }

    private void Add(string key, string path)
    {
        if (PdhAddEnglishCounterW(query, path, UIntPtr.Zero, out var handle) == 0)
            counters[key] = new(handle);
    }

    public bool Collect() => query != IntPtr.Zero && PdhCollectQueryData(query) == 0;

    public IReadOnlyList<CounterReading> Read(string key)
    {
        if (!counters.TryGetValue(key, out var counter)) return [];
        var size = counter.Size;
        uint count = 0;
        var result = PdhGetFormattedCounterArrayW(counter.Handle, 0x200, ref size, ref count, counter.Buffer);
        if (result == 0x800007D2) // PDH_MORE_DATA; instances may appear while playing.
        {
            if (size == 0 || size > 4 * 1024 * 1024) return [];
            if (counter.Buffer != IntPtr.Zero) Marshal.FreeHGlobal(counter.Buffer);
            counter.Buffer = Marshal.AllocHGlobal((int)size);
            counter.Size = size;
            result = PdhGetFormattedCounterArrayW(counter.Handle, 0x200, ref size, ref count, counter.Buffer);
        }
        if (result != 0 || count > 100000 || counter.Buffer == IntPtr.Zero) return [];
        var itemSize = Marshal.SizeOf<FormattedItem>();
        if ((ulong)count * (uint)itemSize > counter.Size) return [];
        var values = new List<CounterReading>((int)count);
        for (var i = 0; i < count; i++)
        {
            var item = Marshal.PtrToStructure<FormattedItem>(IntPtr.Add(counter.Buffer, i * itemSize));
            var name = Marshal.PtrToStringUni(item.Name);
            if (name is not null)
                values.Add(new(name, item.Value.Status is 0 or 1 && double.IsFinite(item.Value.Number) && item.Value.Number >= 0
                    ? item.Value.Number : null));
        }
        return values;
    }

    public void Dispose()
    {
        foreach (var value in counters.Values)
            if (value.Buffer != IntPtr.Zero) Marshal.FreeHGlobal(value.Buffer);
        counters.Clear();
        if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct FormattedValue { public uint Status; public double Number; }
    [StructLayout(LayoutKind.Sequential)] private struct FormattedItem { public IntPtr Name; public FormattedValue Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(string? source, UIntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, UIntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, ref uint count, IntPtr buffer);
}
