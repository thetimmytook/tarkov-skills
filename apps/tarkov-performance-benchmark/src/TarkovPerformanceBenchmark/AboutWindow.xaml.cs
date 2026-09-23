using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace TarkovPerformanceBenchmark;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"}";
    }

    private void GitHub_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/thetimmytook/tarkov-skills") { UseShellExecute = true });

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/thetimmytook/tarkov-skills/blob/main/PRIVACY.md") { UseShellExecute = true });

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
