using System.Diagnostics;
using System.Windows;

namespace TarkovPerformanceToolkit;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();
    private void GitHub_Click(object sender, RoutedEventArgs e) => Open("https://github.com/thetimmytook/tarkov-skills");
    private void Privacy_Click(object sender, RoutedEventArgs e) => Open("https://github.com/thetimmytook/tarkov-skills/blob/main/PRIVACY.md");
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
