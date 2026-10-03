using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Windows.Management.Deployment;

namespace TarkovSkills.StoreRegression;

public enum StoreProduct { Toolkit, Benchmark }

public interface IStoreCommands
{
    string Version { get; }
    Task<CommandOutput> RunAsync(string[] args, int timeoutSeconds = 45);
}

public sealed class StoreAliasClient : IStoreCommands
{
    public string AliasPath { get; }
    public string Version { get; }

    public StoreAliasClient(StoreProduct product = StoreProduct.Toolkit)
    {
        var name = product == StoreProduct.Toolkit ? "Toolkit" : "Benchmark";
        var alias = product == StoreProduct.Toolkit ? "tarkov-skills.exe" : "tarkov-benchmark.exe";
        var package = new PackageManager().FindPackagesForUser("")
            .SingleOrDefault(p => p.Id.Name == "TimmyTook.TarkovPerformance" + name && !p.IsResourcePackage);
        if (package is null) throw new TestBlocked($"{name} Store package is not installed.");
        Check.That(package.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store,
            $"{name} is not Microsoft Store signed.");
        Check.That(package.Id.Architecture == Windows.System.ProcessorArchitecture.X64,
            $"{name} architecture is not x64.");
        var version = package.Id.Version;
        Version = $"{version.Major}.{version.Minor}.{version.Build}";
        AliasPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", alias);
        Check.That(File.Exists(AliasPath), "Store execution alias is missing or disabled.");
        var manifest = XDocument.Load(Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml"));
        Check.That(manifest.Descendants().Any(e => e.Name.LocalName == "ExecutionAlias" &&
            (string?)e.Attribute("Alias") == alias), "Package does not declare the requested alias.");
    }

    public async Task<CommandOutput> RunAsync(string[] args, int timeoutSeconds = 45)
    {
        var start = new ProcessStartInfo(AliasPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new TestFailure("Alias process did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            // Only the child owned by this runner; never a game/external PresentMon process.
            if (!process.HasExited) process.Kill(entireProcessTree: false);
            await process.WaitForExitAsync();
            throw new TestFailure("Command timed out; capture cleanup needs a separate check.");
        }
        return new(process.ExitCode, await stdout, await stderr);
    }

    public static int GuiProcessCount()
    {
        var processes = Process.GetProcessesByName("TarkovPerformanceToolkit")
            .Concat(Process.GetProcessesByName("TarkovPerformanceBenchmark")).ToArray();
        try { return processes.Length; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}

public sealed record CommandOutput(int ExitCode, string Stdout, string Stderr)
{
    public JsonDocument Json()
    {
        Check.That(string.IsNullOrWhiteSpace(Stderr), "Command wrote unexpected stderr.");
        try
        {
            var document = JsonDocument.Parse(Stdout);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new TestFailure("stdout must contain one JSON object.");
            }
            return document;
        }
        catch (JsonException) { throw new TestFailure("stdout is not one valid JSON document."); }
    }
}
