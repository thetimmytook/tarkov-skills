using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using TarkovSkills.Core.Permissions;
using TarkovSkills.Core.Authentication;
using TarkovBenchmark.Feature;

namespace TarkovPerformanceBenchmark;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (EtwPermissions.IsHelperInvocation(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await EtwPermissions.RunHelperAsync(e.Args));
            return;
        }
        _mutex = new Mutex(true, "TimmyTook.TarkovPerformanceBenchmark.SingleInstance", out var first);
        _ownsMutex = first;
        if (!first) { MessageBox.Show("Tarkov Performance Benchmark is already running.", "Already running", MessageBoxButton.OK, MessageBoxImage.Information); Shutdown(20); return; }
        ApplicationFailureHandler.Attach(this, DesktopAuthProduct.Benchmark, typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown");
        var invocation = AppInvocation.Parse(e.Args);
        if (invocation.SourceSkill) NativeConsole.TryAttachToParent();
        MainWindow = new MainWindow(invocation); MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { if (_mutex is not null) { if (_ownsMutex) _mutex.ReleaseMutex(); _mutex.Dispose(); } base.OnExit(e); }
}

public sealed record AppInvocation(bool CollectRequested, bool SourceSkill)
{
    public static AppInvocation Parse(string[] args)
    {
        var collect = args.Any(x => x.Equals("collect", StringComparison.OrdinalIgnoreCase));
        var skill = args.Select((value, index) => (value, index)).Any(x => x.value.Equals("--source", StringComparison.OrdinalIgnoreCase) && x.index + 1 < args.Length && args[x.index + 1].Equals("skill", StringComparison.OrdinalIgnoreCase));
        return new(collect, skill);
    }
}

internal static class NativeConsole
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(uint processId);
    public static void TryAttachToParent() { if (!AttachConsole(0xFFFFFFFF)) return; try { Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true }); } catch { } }
    public static void WriteResult(CommandResult result) { var json = JsonSerializer.Serialize(result, JsonDefaults.Options); try { Console.Out.WriteLine(json); } catch { } AppPaths.WriteLastCommandResult(json); }
}
