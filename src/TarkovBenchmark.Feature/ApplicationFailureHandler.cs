using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TarkovSkills.Core;
using TarkovSkills.Core.Authentication;

namespace TarkovBenchmark.Feature;

// Shared by ordinary GUI hosts only. Elevated helpers never write application state
// or display support UI. Unknown UI failures terminate rather than resume broken state.
public static class ApplicationFailureHandler
{
    public static void Attach(Application application, DesktopAuthProduct product, string version)
    {
        var handling = false;
        application.DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (handling) { application.Shutdown(1); return; }
            handling = true;
            try
            {
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { application.MainWindow?.Close(); } catch { }
                Show(SupportArchive.Create(product, version, SupportOperation.Application, args.Exception), null,
                    "The application encountered an error and needs to close.");
            }
            catch { /* Reporting must not recursively crash. The local report may still be available. */ }
            finally { application.Shutdown(1); }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.IsTerminating && args.ExceptionObject is Exception error)
            {
                try { SupportArchive.MarkPending(SupportArchive.Create(product, version, SupportOperation.Application, error)); }
                catch { }
            }
        };
        EventHandler? activated = null;
        activated = (_, _) =>
        {
            application.Activated -= activated;
            application.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SupportArchive.TakePending() is { } bundle)
                    Show(bundle, application.MainWindow, "The previous application session ended unexpectedly.");
            }));
        };
        application.Activated += activated;
    }

    private static void Show(SupportBundle bundle, Window? owner, string message)
    {
        var content = new StackPanel { Margin = new Thickness(24) };
        var support = new FailureSupportView { Margin = new Thickness(0, 16, 0, 0) };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(support);
        var window = new Window
        {
            Title = "Application error", Width = 620, MinWidth = 430, SizeToContent = SizeToContent.Height,
            MaxHeight = SystemParameters.WorkArea.Height, Owner = owner, WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Background = (Brush)Application.Current.FindResource("BackgroundBrush"),
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
        };
        var close = new Button { Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0) };
        close.Click += (_, _) => window.Close(); content.Children.Add(close);
        support.ShowBundle(bundle);
        window.ShowDialog();
    }
}
