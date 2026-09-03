using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovPerformanceToolkit;

public partial class MainWindow : Window
{
    private string? _reportJson;

    public MainWindow()
    {
        InitializeComponent();
        var goal = new GoalStore().Load();
        GoalText.Text = goal.Goal;
        TargetText.Text = goal.TargetFpsMin.ToString();
        QualityText.Text = goal.QualityPreference;
        var ready = new PresentMonRunner().IsDependencyReady(out var message);
        DetailText.Text = ready ? $"{message}. No data is uploaded automatically." : message;
        BenchmarkRoot.Content = new BenchmarkView(BenchmarkFeatureOptions.ForToolkit(
            typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
    }

    private void Inspect_Click(object sender, RoutedEventArgs e) => SetReport(new InspectionService().Inspect(), "Report collected from read-only local sources.");
    private void SaveGoal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(TargetText.Text, out var target)) throw new ArgumentException("Target FPS must be a number.");
            var goal = new GoalStore().Save(GoalText.Text, target, QualityText.Text);
            ShowStatus($"Goal saved: {goal.TargetFpsMin} FPS.", false);
        }
        catch (Exception ex) { ShowStatus(ex.Message, true); }
    }
    private void Copy_Click(object sender, RoutedEventArgs e) { if (_reportJson is null) return; Clipboard.SetText(_reportJson); ShowStatus("Sanitized JSON copied to the clipboard.", false); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_reportJson is null) return;
        var dialog = new SaveFileDialog { FileName = "tarkov-performance-report.json", Filter = "JSON report (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true) { File.WriteAllText(dialog.FileName, _reportJson); ShowStatus("JSON report saved.", false); }
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { AppPaths.EnsureDataDirectory(); Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true }); }
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void SetReport(object report, string status) { _reportJson = JsonSerializer.Serialize(report, JsonDefaults.Options); ReportText.Text = _reportJson; CopyButton.IsEnabled = true; SaveButton.IsEnabled = true; ShowStatus(status, false); }
    private void ShowStatus(string text, bool warning) { StatusText.Text = text; StatusText.Foreground = (Brush)FindResource(warning ? "WarningBrush" : "TextBrush"); }
}
