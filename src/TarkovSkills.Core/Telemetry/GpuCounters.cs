using System.Text.RegularExpressions;

namespace TarkovSkills.Core;

internal static partial class GpuCounters
{
    [GeneratedRegex(@"^pid_(\d+)_luid_(0x[0-9a-f]+)_(0x[0-9a-f]+)_phys_(\d+)_eng_(\d+)_engtype_3D$", RegexOptions.IgnoreCase)]
    private static partial Regex EngineName();
    [GeneratedRegex(@"^luid_(0x[0-9a-f]+)_(0x[0-9a-f]+)_phys_(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryName();

    internal static string Key(uint high, uint low) => $"{high:x8}:{low:x8}";
    private static string Key(string high, string low) => Key(Convert.ToUInt32(high[2..], 16), Convert.ToUInt32(low[2..], 16));

    public static GpuSample Parse(IReadOnlyList<CounterReading> engines, IReadOnlyList<CounterReading> dedicated,
        IReadOnlyList<CounterReading> shared, int gamePid)
    {
        var graphics = engines.Select(e => (Reading: e, Match: EngineName().Match(e.Instance)))
            .Where(e => e.Match.Success).ToArray();
        var gameAdapters = graphics.Where(e => e.Match.Groups[1].Value == gamePid.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                e.Reading.Value > 0 && e.Match.Groups[4].Value == "0")
            .Select(e => Key(e.Match.Groups[2].Value, e.Match.Groups[3].Value)).Distinct().ToArray();
        var loads = new Dictionary<string, double?>();
        foreach (var adapter in graphics.GroupBy(e => Key(e.Match.Groups[2].Value, e.Match.Groups[3].Value)))
        {
            // Linked physical nodes are not combined or silently assigned node zero.
            if (adapter.Any(e => e.Match.Groups[4].Value != "0")) { loads[adapter.Key] = null; continue; }
            var engineLoads = adapter.GroupBy(e => e.Match.Groups[5].Value).Select(engine =>
            {
                var sum = engine.Sum(e => e.Reading.Value ?? 0);
                return engine.All(e => e.Reading.Value.HasValue) && sum <= 100 ? (double?)sum : null;
            }).ToArray();
            loads[adapter.Key] = engineLoads.All(v => v.HasValue) ? engineLoads.Max() : null;
        }
        var dedicatedMemory = ParseMemory(dedicated);
        var sharedMemory = ParseMemory(shared);
        var unsupported = graphics.Where(e => e.Match.Groups[4].Value != "0")
            .Select(e => Key(e.Match.Groups[2].Value, e.Match.Groups[3].Value))
            .Concat(dedicated.Concat(shared).Select(c => MemoryName().Match(c.Instance))
                .Where(m => m.Success && m.Groups[3].Value != "0").Select(m => Key(m.Groups[1].Value, m.Groups[2].Value)))
            .Distinct().ToArray();
        var keys = loads.Keys.Concat(dedicatedMemory.Keys).Concat(sharedMemory.Keys).Distinct();
        return new(keys.ToDictionary(key => key, key => new GpuReading(loads.GetValueOrDefault(key),
            dedicatedMemory.GetValueOrDefault(key), sharedMemory.GetValueOrDefault(key))), gameAdapters, unsupported);
    }

    private static Dictionary<string, double?> ParseMemory(IReadOnlyList<CounterReading> counters) => counters
        .Select(c => (Reading: c, Match: MemoryName().Match(c.Instance))).Where(c => c.Match.Success)
        .GroupBy(c => Key(c.Match.Groups[1].Value, c.Match.Groups[2].Value))
        .ToDictionary(group => group.Key, group => group.Count() == 1 && group.First().Match.Groups[3].Value == "0"
            ? group.First().Reading.Value : null);
}
