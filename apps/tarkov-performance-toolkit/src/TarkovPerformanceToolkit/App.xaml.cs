using System.Windows;
using TarkovSkills.Core.Permissions;
using TarkovSkills.Core.Authentication;
using TarkovBenchmark.Feature;

namespace TarkovPerformanceToolkit;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (EtwPermissions.IsHelperInvocation(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await EtwPermissions.RunHelperAsync(e.Args));
            return;
        }
        ApplicationFailureHandler.Attach(this, DesktopAuthProduct.Toolkit, typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown");
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
