using System.Runtime.InteropServices;

namespace TarkovSkills.Core.Tests;

[CollectionDefinition("Vendor native lifetime", DisableParallelization = true)]
public sealed class VendorNativeCollection;

[Collection("Vendor native lifetime")]
public sealed class VendorGpuNativeTests
{
    [Fact]
    public void NvidiaMatchesSignedLuidRatherThanNameOrdinalOrBusiestCard()
    {
        var native = new FakeNvApi();
        var key = GpuCounters.Key(uint.MaxValue, 1);
        var provider = new NvApiGpuUsage([new(key, "Identical model", 8, false, 0x10de),
            new(GpuCounters.Key(0, 2), "Identical model", 8, false, 0x1002)], native);
        var readings = provider.Read();
        Assert.Equal(key, readings.Keys.Single()); Assert.Equal(25, readings[key].Percent);
        Assert.Equal(new nint[] { 10 }, native.ReadHandles);
        Assert.Equal(72u | 1u << 16, native.UsageVersion);
        Assert.Equal(568u | 1u << 16, native.MappingVersion);
        provider.Dispose(); provider.Dispose();
        Assert.Equal(1, native.Unloads); Assert.Equal(1, native.DisposeCount);
        Assert.Empty(provider.Read());
    }

    [Theory]
    [InlineData("missing-export")] [InlineData("initialize")] [InlineData("enumerate")]
    [InlineData("linked")] [InlineData("duplicate-luid")] [InlineData("mapping")]
    public void NvidiaUnsupportedMappingOrInitializationNeverGuessesAnAdapter(string scenario)
    {
        var native = new FakeNvApi { Scenario = scenario };
        using var provider = new NvApiGpuUsage(NvAdapters(), native);
        Assert.Empty(provider.Read());
        Assert.Equal(1, native.DisposeCount);
        Assert.Equal(scenario is "missing-export" or "initialize" ? 0 : 1, native.Unloads);
    }

    [Theory]
    [InlineData("not-present")] [InlineData("invalid")] [InlineData("query-error")]
    public void NvidiaOneFailedGpuDoesNotEraseAnotherGpuOrCreateMeasuredZero(string scenario)
    {
        var native = new FakeNvApi { Scenario = scenario };
        using var provider = new NvApiGpuUsage(NvAdapters(), native);
        var samples = provider.Read();
        Assert.Null(samples[GpuCounters.Key(uint.MaxValue, 1)].Percent);
        Assert.Equal(95, samples[GpuCounters.Key(0, 2)].Percent);
        native.Scenario = "zero";
        Assert.Equal(0, provider.Read()[GpuCounters.Key(uint.MaxValue, 1)].Percent);
    }

    [Fact]
    public void AmdUsesGpu2LuidByteCapabilityAndReleasesOwnedInterfacesBeforeTerminate()
    {
        var native = new FakeAdlx();
        var provider = new AdlxGpuUsage(AmdAdapters(), native);
        var sample = provider.Read().Single();
        Assert.Equal(GpuCounters.Key(0, 7), sample.Key); Assert.Equal(80, sample.Value.Percent);
        Assert.Equal(1000, sample.Value.DriverTimestamp);
        Assert.Equal("IADLXGPU2", native.QueriedInterface);
        Assert.Equal(native.FullVersion, native.InitializedVersion);
        Assert.Equal(1, native.Starts); Assert.Equal(0, native.Stops);
        provider.Dispose(); provider.Dispose();
        Assert.Equal(1, native.Stops); Assert.Equal(1, native.Terminates); Assert.Equal(1, native.DisposeCount);
        Assert.True(native.Events.IndexOf("stop") < native.Events.IndexOf("release-gpu-0"));
        Assert.True(native.Events.IndexOf("release-service") < native.Events.IndexOf("terminate"));
        Assert.Equal(1, native.Events.Count(e => e == "release-gpu-0"));
        Assert.DoesNotContain("release-system", native.Events); Assert.Empty(provider.Read());
    }

    [Theory]
    [InlineData("gpu2-missing")] [InlineData("unknown-luid")] [InlineData("unsupported")]
    [InlineData("linked")] [InlineData("start-error")] [InlineData("duplicate-luid")]
    public void AmdUnsupportedOrAmbiguousGpuCleansUpWithoutUsingAnOrdinalFallback(string scenario)
    {
        var native = new FakeAdlx(scenario == "duplicate-luid" ? 2 : 1) { Scenario = scenario };
        using var provider = new AdlxGpuUsage(AmdAdapters(), native);
        Assert.Empty(provider.Read());
        Assert.Equal(1, native.DisposeCount); Assert.Equal(1, native.Terminates); Assert.Equal(0, native.Stops);
        Assert.Equal(scenario == "start-error" ? 1 : 0, native.Starts);
        Assert.Equal(scenario == "duplicate-luid" ? 2 : 1, native.Events.Count(e => e.StartsWith("release-gpu-", StringComparison.Ordinal) && char.IsDigit(e[^1])));
    }

    [Fact]
    public void AmdAlreadyInitializedForeignRuntimeIsNotTerminated()
    {
        var native = new FakeAdlx { Scenario = "foreign-runtime" };
        using var provider = new AdlxGpuUsage(AmdAdapters(), native);
        Assert.Empty(provider.Read()); Assert.Equal(0, native.Starts); Assert.Equal(0, native.Terminates);
        Assert.Equal(1, native.DisposeCount);
    }

    [Fact]
    public void AmdSecondOwnerFailsSafelyAndCanInitializeAfterTheFirstIsDisposed()
    {
        var firstNative = new FakeAdlx();
        var first = new AdlxGpuUsage(AmdAdapters(), firstNative);
        Assert.Single(first.Read());
        var secondNative = new FakeAdlx();
        using (var second = new AdlxGpuUsage(AmdAdapters(), secondNative)) Assert.Empty(second.Read());
        Assert.Equal(0, secondNative.Starts); Assert.Equal(0, secondNative.Terminates);
        first.Dispose();
        var thirdNative = new FakeAdlx();
        using var third = new AdlxGpuUsage(AmdAdapters(), thirdNative);
        Assert.Single(third.Read());
    }

    [Theory]
    [InlineData("history-error")] [InlineData("timestamp-error")] [InlineData("invalid-usage")]
    public void AmdNativeFailuresRemainUnknownAndHistoryObjectsAreReleased(string scenario)
    {
        var native = new FakeAdlx { Scenario = scenario };
        using var provider = new AdlxGpuUsage(AmdAdapters(), native);
        var reading = provider.Read().Single().Value;
        Assert.Null(reading.Percent);
        if (scenario != "history-error")
        {
            Assert.Contains("release-metric", native.Events); Assert.Contains("release-history", native.Events);
        }
    }

    private static AdapterReading[] NvAdapters() => [new(GpuCounters.Key(uint.MaxValue, 1), "Same model", 8, false, 0x10de),
        new(GpuCounters.Key(0, 2), "Same model", 8, false, 0x10de)];
    private static AdapterReading[] AmdAdapters() => [new(GpuCounters.Key(0, 7), "Same model", 8, false, 0x1002)];

    private abstract class FakeLibrary : IWindowsDriverLibrary
    {
        private readonly List<Delegate> delegates = [];
        private readonly List<nint> allocations = [];
        protected readonly Dictionary<string, nint> exports = [];
        internal readonly List<string> Events = [];
        internal int DisposeCount;
        public nint Handle => DisposeCount == 0 ? 1 : 0;
        public virtual nint Export(string name) => exports.GetValueOrDefault(name);
        protected nint Root(Delegate value) { delegates.Add(value); return Marshal.GetFunctionPointerForDelegate(value); }
        protected nint Object(string name, params (int Slot, Delegate Method)[] methods)
        {
            var slots = Math.Max(2, methods.Length == 0 ? 0 : methods.Max(m => m.Slot) + 1);
            var table = Marshal.AllocHGlobal(slots * nint.Size);
            var pointer = Marshal.AllocHGlobal(nint.Size);
            allocations.Add(table); allocations.Add(pointer);
            for (int slot = 0; slot < slots; slot++) Marshal.WriteIntPtr(table, slot * nint.Size, 0);
            if (name != "system") Marshal.WriteIntPtr(table, nint.Size, Root(new AdlxCalls.Reference(_ => { Events.Add("release-" + name); return 0; })));
            foreach (var method in methods) Marshal.WriteIntPtr(table, method.Slot * nint.Size, Root(method.Method));
            Marshal.WriteIntPtr(pointer, table); return pointer;
        }
        public void Dispose()
        {
            DisposeCount++;
            if (DisposeCount != 1) return;
            foreach (var allocation in allocations) Marshal.FreeHGlobal(allocation);
        }
    }

    private sealed class FakeNvApi : FakeLibrary
    {
        private readonly Dictionary<uint, nint> functions = [];
        internal string Scenario = "ok";
        internal int Unloads;
        internal uint UsageVersion, MappingVersion;
        internal readonly List<nint> ReadHandles = [];
        internal FakeNvApi()
        {
            functions[0x0150e828] = Root(new NvApiGpuUsage.Simple(() => Scenario == "initialize" ? -1 : 0));
            functions[0xd22bdd7e] = Root(new NvApiGpuUsage.Simple(() => { Unloads++; return 0; }));
            functions[0xe5ac921f] = Root(new NvApiGpuUsage.Enumerate((nint[] handles, out uint count) =>
            { handles[0] = 10; handles[1] = 20; count = Scenario == "enumerate" ? 65u : 2; return 0; }));
            functions[0xadd604d1] = Root(new NvApiGpuUsage.LogicalHandle((nint gpu, out nint logical) => { logical = gpu; return 0; }));
            functions[0x842b066e] = Root(new NvApiGpuUsage.LogicalInfo((nint gpu, ref NvApiGpuUsage.LogicalData data) =>
            {
                MappingVersion = data.Version;
                Assert.All(data.Reserved, value => Assert.Equal(0u, value));
                data.PhysicalCount = Scenario == "linked" ? 2u : 1;
                data.PhysicalHandles[0] = gpu;
                Marshal.StructureToPtr(gpu == 10 || Scenario == "duplicate-luid" ? new GpuLuid { High = -1, Low = 1 } :
                    new GpuLuid { Low = 2 }, data.OsAdapterId, false);
                return Scenario == "mapping" ? -1 : 0;
            }));
            functions[0x60ded2ed] = Root(new NvApiGpuUsage.DynamicStates((nint gpu, ref NvApiGpuUsage.States data) =>
            {
                ReadHandles.Add(gpu); UsageVersion = data.Version;
                data.Domains[0] = new() { Present = gpu == 10 && Scenario == "not-present" ? 0u : 1u,
                    Percent = gpu != 10 ? 95u : Scenario == "invalid" ? 101u : Scenario == "zero" ? 0u : 25u };
                return gpu == 10 && Scenario == "query-error" ? -1 : 0;
            }));
            exports["nvapi_QueryInterface"] = Root(new NvApiGpuUsage.QueryInterface(id =>
                Scenario == "missing-export" && id == 0x0150e828 ? 0 : functions.GetValueOrDefault(id)));
        }
    }

    private sealed class FakeAdlx : FakeLibrary
    {
        internal string Scenario = "ok", QueriedInterface = "";
        internal ulong FullVersion = 0x000100050000007c, InitializedVersion;
        internal int Starts, Stops, Terminates;
        internal FakeAdlx(int gpuCount = 1)
        {
            var metric = Object("metric",
                (3, new AdlxCalls.Timestamp((nint _, out long value) => { value = 1000; return Scenario == "timestamp-error" ? -1 : 0; })),
                (4, new AdlxCalls.Usage((nint _, out double value) => { value = Scenario == "invalid-usage" ? double.NaN : 80; return 0; })));
            var history = Object("history", (3, new AdlxCalls.Count(_ => 1)),
                (11, new AdlxCalls.At((nint _, uint index, out nint value) => { Assert.Equal(0u, index); value = metric; return 0; })));
            var caps = Object("caps", (3, new AdlxCalls.Boolean((nint _, out byte value) => { value = Scenario == "unsupported" ? (byte)0 : (byte)1; return 0; })));
            var gpus = new List<nint>();
            for (int index = 0; index < gpuCount; index++)
            {
                var gpu2 = Object("gpu2",
                    (21, new AdlxCalls.Integer((nint _, out int value) => { value = Scenario == "linked" ? 1 : 0; return 0; })),
                    (34, new AdlxCalls.Luid((nint _, out GpuLuid value) => { value = new() { Low = Scenario == "unknown-luid" ? 999u : 7u }; return 0; })));
                gpus.Add(Object("gpu-" + index, (2, new AdlxCalls.Query((nint _, string id, out nint value) =>
                { QueriedInterface = id; value = Scenario == "gpu2-missing" ? 0 : gpu2; return value == 0 ? -1 : 0; }))));
            }
            var list = Object("gpu-list", (3, new AdlxCalls.Count(_ => (uint)gpus.Count)),
                (11, new AdlxCalls.At((nint _, uint index, out nint value) => { value = gpus[(int)index]; return 0; })));
            var service = Object("service",
                (11, new AdlxCalls.Reference(_ => { Starts++; Events.Add("start"); return Scenario == "start-error" ? -1 : 0; })),
                (12, new AdlxCalls.Reference(_ => { Stops++; Events.Add("stop"); return 0; })),
                (14, new AdlxCalls.History((nint _, nint gpu, int start, int end, out nint value) =>
                { Assert.Contains(gpu, gpus); Assert.Equal(0, start); Assert.Equal(0, end); value = Scenario == "history-error" ? 0 : history; return value == 0 ? -1 : 0; })),
                (21, new AdlxCalls.Support((nint _, nint gpu, out nint value) => { Assert.Contains(gpu, gpus); value = caps; return 0; })));
            var system = Object("system", (1, new AdlxCalls.Object((nint _, out nint value) => { value = list; return 0; })),
                (9, new AdlxCalls.Object((nint _, out nint value) => { value = service; return 0; })));
            exports["ADLXQueryFullVersion"] = Root(new AdlxCalls.FullVersion((out ulong version) => { version = FullVersion; return 0; }));
            exports["ADLXInitialize"] = Root(new AdlxCalls.Initialize((ulong version, out nint value) =>
            { InitializedVersion = version; value = system; return Scenario == "foreign-runtime" ? -1 : 0; }));
            exports["ADLXTerminate"] = Root(new AdlxCalls.Terminate(() => { Terminates++; Events.Add("terminate"); return 0; }));
        }
    }
}
