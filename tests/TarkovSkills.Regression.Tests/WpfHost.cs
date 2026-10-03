using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TarkovSkills.Core;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TarkovSkills.Regression.Tests;

internal static class TestData
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "tarkov-ui-regression-" + Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Isolate() => Environment.SetEnvironmentVariable("TARKOV_BENCHMARK_TEST_DATA_DIRECTORY", Root);
}

[CollectionDefinition("WPF")]
public sealed class WpfCollection : ICollectionFixture<WpfHost>;

public sealed class WpfHost : IDisposable
{
    private readonly Thread thread;
    private readonly Dispatcher dispatcher;

    public WpfHost()
    {
        // Fail closed before constructing any product UI if isolated AppPaths is unavailable.
        if (AppPaths.DataDirectory != TestData.Root) throw new InvalidOperationException("UI test state was not isolated.");
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/TarkovBenchmark.Feature;component/Themes/TimmyAcademy.xaml", UriKind.Relative)
                });
                ready.SetResult(app.Dispatcher);
                app.Run();
            }
            catch (Exception exception) { ready.TrySetException(exception); }
        }) { IsBackground = true, Name = "Isolated WPF regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        dispatcher = ready.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
    }

    public void Run(Action test) => dispatcher.Invoke(test);

    public static FrameworkElement Layout(Window window, double width, double height)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        content.UpdateLayout();
        return content;
    }

    public void Dispose()
    {
        dispatcher.Invoke(() => Application.Current.Shutdown());
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new InvalidOperationException("WPF dispatcher did not stop.");
        // Only the uniquely owned test directory; never package LocalState or user history.
        if (Directory.Exists(TestData.Root)) Directory.Delete(TestData.Root, recursive: true);
    }
}
