using System.Windows;

namespace TarkovBenchmark.Feature;

public partial class SubmissionWindow : Window
{
    public SubmissionWindow(int runCount) { InitializeComponent(); SummaryText.Text = $"JSON for {runCount} run(s) was copied to your clipboard."; }
    private void OpenForm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
