using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;
using TarkovSkills.Core.Authentication;

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
        var ready = new PresentMonRunner().IsDependencyReady(out var message);
        DetailText.Text = ready ? $"{message}. No data is uploaded automatically." : message;
        if (CaptureDiagnostics.Enabled)
        {
            Title += $" · {CaptureDiagnostics.BuildLabel}";
            DetailText.Text += " Diagnostic logs: Open data folder → reports. Logs stay on this PC.";
        }
        var benchmark = new BenchmarkView(BenchmarkFeatureOptions.ForToolkit(
            typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        BenchmarkRoot.Content = benchmark;
        SettingsRoot.Content = new SettingsView(benchmark.CaptureAccess, SettingsSections.Access | SettingsSections.Goal);
        benchmark.CaptureStateChanged += (_, _) => SettingsTab.IsEnabled = !benchmark.IsCollecting;
        Closed += (_, _) => benchmark.CaptureAccess.Dispose();
        ContentHeightLimit.Attach(this, BenchmarkScroll, benchmark);
    }

    private void Inspect_Click(object sender, RoutedEventArgs e)
    {
        try { SetReport(new InspectionService().Inspect(), "Report ready. Copy JSON and paste it into your chat for analysis."); OverviewFailure.Clear(); }
        catch (Exception error) { ShowFailure(SupportOperation.Read, error); }
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_reportJson is null) return;
        try { Clipboard.SetText(_reportJson); ShowStatus("Sanitized JSON copied to the clipboard.", false); }
        catch (Exception error) { ShowFailure(SupportOperation.Save, error); }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_reportJson is null) return;
        var dialog = new SaveFileDialog { FileName = "tarkov-performance-report.json", Filter = "JSON report (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true)
        {
            try { File.WriteAllText(dialog.FileName, _reportJson); ShowStatus("JSON report saved.", false); OverviewFailure.Clear(); }
            catch (Exception error) { ShowFailure(SupportOperation.Save, error); }
        }
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
    private void ShowFailure(SupportOperation operation, Exception error)
    {
        ShowStatus("The operation could not be completed. You can report the problem below.", true);
        OverviewFailure.ShowFailure(DesktopAuthProduct.Toolkit, typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown", operation, error);
        OverviewScroll.ScrollToTop();
    }
}
