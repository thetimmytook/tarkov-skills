using System.Diagnostics;
using System.Windows;
using TarkovBenchmark.Feature;

namespace TarkovPerformanceToolkit;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown"}";
    }
    private void GitHub_Click(object sender, RoutedEventArgs e) => Open("https://github.com/thetimmytook/tarkov-skills");
    private void Privacy_Click(object sender, RoutedEventArgs e) => Open("https://github.com/thetimmytook/tarkov-skills/blob/main/PRIVACY.md");
    private void SkillsGuide_Click(object sender, RoutedEventArgs e) => SkillsGuide.Open(this);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
