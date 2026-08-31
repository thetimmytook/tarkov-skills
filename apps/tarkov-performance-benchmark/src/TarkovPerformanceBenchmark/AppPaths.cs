using System.Text;

namespace TarkovPerformanceBenchmark;

internal static class AppPaths
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TarkovSkills");
    public static string BenchmarkFile => Path.Combine(DataDirectory, "benchmark.json");
    public static string LastCommandResultFile => Path.Combine(DataDirectory, "last-command-result.json");
    public static string ReportsDirectory => Path.Combine(DataDirectory, "reports");
    public static string PresentMonFile => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe");
    public static string PresentMonManifestFile => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "dependency.json");
    public static string TarkovSettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
    public static void WriteLastCommandResult(string json) { EnsureDataDirectory(); AtomicWrite(LastCommandResultFile, json); }
    public static void AtomicWrite(string path, string contents) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp"; File.WriteAllText(temp, contents, new UTF8Encoding(false)); File.Move(temp, path, true); }
}
