using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TarkovSkills.Core;

public static class AppPaths
{
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15700;

    public static string DataDirectory { get; } = ResolveDataDirectory(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        GetPackageFamilyName());
    public static string BenchmarkFile => Path.Combine(DataDirectory, "benchmark.json");
    public static string LastCommandResultFile => Path.Combine(DataDirectory, "last-command-result.json");
    public static string ReportsDirectory => Path.Combine(DataDirectory, "reports");
    public static string PresentMonFile => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe");
    public static string PresentMonManifestFile => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "dependency.json");
    public static string TarkovSettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
    public static void WriteLastCommandResult(string json) { EnsureDataDirectory(); AtomicWrite(LastCommandResultFile, json); }
    public static void AtomicWrite(string path, string contents) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp"; File.WriteAllText(temp, contents, new UTF8Encoding(false)); File.Move(temp, path, true); }

    internal static string ResolveDataDirectory(string localAppData, string? packageFamilyName) =>
        string.IsNullOrWhiteSpace(packageFamilyName)
            ? Path.Combine(localAppData, "TarkovSkills")
            : Path.Combine(localAppData, "Packages", packageFamilyName, "LocalState", "TarkovSkills");

    private static string? GetPackageFamilyName()
    {
        uint length = 0;
        var result = GetCurrentPackageFamilyName(ref length, null);
        if (result == AppModelErrorNoPackage) return null;
        if (result != ErrorInsufficientBuffer) throw new Win32Exception(result, "Windows package identity could not be read.");

        var value = new StringBuilder((int)length);
        result = GetCurrentPackageFamilyName(ref length, value);
        if (result != 0) throw new Win32Exception(result, "Windows package identity could not be read.");
        return value.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);
}
