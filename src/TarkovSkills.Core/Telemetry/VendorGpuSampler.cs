namespace TarkovSkills.Core;

internal sealed class VendorGpuSampler : IDisposable
{
    private readonly IReadOnlyList<AdapterReading> inventory;
    private readonly List<(IGpuUsageProvider Provider, Dictionary<string, GpuUsageObservations> Observations)> providers = [];
    private bool disposed;

    internal VendorGpuSampler(IReadOnlyList<AdapterReading> inventory,
        Func<string, IReadOnlyList<AdapterReading>, IGpuUsageProvider>? factory = null)
    {
        this.inventory = inventory;
        factory ??= (source, adapters) => source == GpuUsageSources.Nvidia ? new NvApiGpuUsage(adapters) : new AdlxGpuUsage(adapters);
        foreach (var source in inventory.Select(a => GpuUsageSources.For(a.VendorId)).Where(GpuUsageSources.IsVendor).Distinct())
        {
            IGpuUsageProvider? provider = null;
            try
            {
                provider = factory(source, inventory);
                if (provider.Source != source) { provider.Dispose(); continue; }
                providers.Add((provider, []));
            }
            catch { try { provider?.Dispose(); } catch { } }
        }
    }

    internal IReadOnlyDictionary<string, VendorGpuReading> Poll()
    {
        var values = new Dictionary<string, VendorGpuReading>();
        if (disposed) return values;
        foreach (var (provider, observations) in providers)
        {
            try
            {
                foreach (var (key, raw) in provider.Read())
                {
                    if (inventory.Count(a => a.Key == key && GpuUsageSources.For(a.VendorId) == provider.Source) != 1) continue;
                    if (!observations.TryGetValue(key, out var observer))
                        observations[key] = observer = new(provider.Source == GpuUsageSources.Amd);
                    if (observer.Accept(raw) is { } reading) values[key] = reading;
                }
            }
            catch { observations.Clear(); } // Other providers/CPU/memory continue.
        }
        return values;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var (provider, observations) in providers)
        {
            try { provider.Dispose(); } catch { }
            observations.Clear();
        }
        providers.Clear();
    }
}
