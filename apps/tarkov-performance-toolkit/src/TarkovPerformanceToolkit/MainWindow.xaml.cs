using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovPerformanceToolkit;

public partial class MainWindow : Window
{
    private string? _reportJson;
    private readonly ToolkitPreferencesStore _preferencesStore = new();
    private ToolkitPreferences _preferences;

    public MainWindow()
    {
        InitializeComponent();
        _preferences = _preferencesStore.Load();
        UpdateGettingStarted();
        var goal = new GoalStore().Load();
        GoalText.Text = goal.Goal;
        TargetText.Text = goal.TargetFpsMin.ToString();
        QualityText.Text = goal.QualityPreference;
        var ready = new PresentMonRunner().IsDependencyReady(out var message);
        DetailText.Text = ready ? $"{message}. No data is uploaded automatically." : message;
        BenchmarkRoot.Content = new BenchmarkView(BenchmarkFeatureOptions.ForToolkit(
            typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        ContentHeightLimit.Attach(this, BenchmarkScroll, (BenchmarkView)BenchmarkRoot.Content);
    }

    private void Inspect_Click(object sender, RoutedEventArgs e) => SetReport(new InspectionService().Inspect(), "Report ready. Copy JSON and paste it into your chat for analysis.");
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
    private void SkillsGuide_Click(object sender, RoutedEventArgs e) => SkillsGuide.Open(this);
    private void ToggleGettingStarted_Click(object sender, RoutedEventArgs e)
    {
        _preferences = _preferences with { ShowGetStarted = !_preferences.ShowGetStarted };
        UpdateGettingStarted();
        if (_preferences.ShowGetStarted) OverviewScroll.ScrollToTop();
        else GettingStartedButton.Focus();
        try { _preferencesStore.Save(_preferences); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowStatus("Getting started changed for this session, but the preference could not be saved.", true);
        }
    }

    private void UpdateGettingStarted()
    {
        GettingStartedCard.Visibility = _preferences.ShowGetStarted ? Visibility.Visible : Visibility.Collapsed;
        var label = _preferences.ShowGetStarted ? "Hide getting started" : "Show getting started";
        GettingStartedButton.ToolTip = label;
        AutomationProperties.SetName(GettingStartedButton, label);
    }

    private void SetReport(object report, string status) { _reportJson = JsonSerializer.Serialize(report, JsonDefaults.Options); ReportText.Text = _reportJson; CopyButton.IsEnabled = true; SaveButton.IsEnabled = true; ShowStatus(status, false); }
    private void ShowStatus(string text, bool warning) { StatusText.Text = text; StatusText.Foreground = (Brush)FindResource(warning ? "WarningBrush" : "TextBrush"); }
}
