using System.Windows;
using TarkovBenchmark.Feature;

namespace TarkovPerformanceBenchmark;

public partial class MainWindow : Window
{
    public MainWindow(AppInvocation invocation)
    {
        InitializeComponent();
        var options = BenchmarkFeatureOptions.ForStandalone(
            typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            invocation.CollectRequested,
            invocation.SourceSkill,
            NativeConsole.WriteResult);
        var view = new BenchmarkView(options);
        view.RequestClose += (_, _) => Close();
        ContentRoot.Content = view;
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
}
