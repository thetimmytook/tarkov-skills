using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TarkovPerformanceBenchmark;

public partial class MainWindow : Window
{
    private const int CaptureDurationSeconds = 120;
    private const string PerformanceFormUrl = "https://forms.gle/D692T2Umd5ktD5wj8";
    private readonly AppInvocation _invocation;
    private readonly PresentMonRunner _presentMon = new();
    private readonly RaidLogReader _raidLogs = new();
    private readonly BenchmarkStore _store = new();
    private CancellationTokenSource? _captureCancellation;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Stopwatch? _captureClock;
    private bool _completedCommand;
    private bool _waitingForSubmissionReturn;
    private bool _submissionWindowLostFocus;
    private IReadOnlyList<string> _pendingSubmissionRunIds = [];

    public MainWindow(AppInvocation invocation)
    {
        InitializeComponent(); _invocation = invocation; Loaded += OnLoaded; Closing += OnClosing;
        Activated += SubmissionWindow_Activated;
        Deactivated += SubmissionWindow_Deactivated;
        _timer.Tick += (_, _) => { if (_captureClock is not null) SetStatus($"Collecting frametime data: {_captureClock.Elapsed:mm\\:ss} / 02:00", false); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshDependency(); LoadLatestResult();
        if (_invocation.CollectRequested) SetStatus("Ready for a skill-requested benchmark. Start the raid, then press Start collection.", false);
        else if (TarkovState.FindProcess() is null) SetStatus("Run Tarkov and enter a raid to collect performance data.", true);
        else SetStatus("Tarkov is running. Enter a raid, then press Start collection.", false);
    }

    private void RefreshDependency()
    {
        var ready = _presentMon.IsDependencyReady(out var message); PresentMonText.Text = message; PresentMonDot.Fill = (Brush)FindResource(ready ? "ReadyBrush" : "ErrorBrush"); StartButton.IsEnabled = ready;
        if (!ready) SetStatus("The bundled performance collector failed its integrity check.", true);
    }

    private void LoadLatestResult()
    {
        var fileExists = File.Exists(AppPaths.BenchmarkFile);
        OpenFolderButton.IsEnabled = fileExists;
        try
        {
            var document = _store.Load();
            UpdateRunCount(document.Runs.Count);
            SubmitButton.IsEnabled = document.Runs.Count > 0;
            var latest = document.Runs.LastOrDefault();
            if (latest is not null) DisplayMetrics(latest.Performance);
        }
        catch
        {
            SubmitButton.IsEnabled = false;
            UpdateRunCount(0);
            StatusDetailText.Text = "Existing benchmark data uses an unsupported prototype format.";
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var tarkov = TarkovState.FindProcess();
        if (tarkov is null) { ShowInfo("Run Tarkov and enter a raid, then try again."); return; }
        RaidContext initialContext;
        try { initialContext = _raidLogs.Read(tarkov.StartTime); }
        catch (Exception ex) { ShowInfo($"Tarkov logs could not be read: {ex.Message}"); return; }
        if (!initialContext.Active) { ShowInfo("Now start the raid, then try again."); return; }

        _completedCommand = false; SetCollecting(true); SetStatus("Preparing benchmark...", false); _captureCancellation = new CancellationTokenSource();
        try
        {
            var settingsTask = Task.Run(() => new SettingsReader().Read());
            var systemTask = Task.Run(() => new SystemInfoCollector().Collect());
            await Task.WhenAll(settingsTask, systemTask);
            var metrics = await _presentMon.CaptureAsync(CaptureDurationSeconds, () => Dispatcher.Invoke(() => { _captureClock = Stopwatch.StartNew(); _timer.Start(); SetStatus("Collecting frametime data: 00:00 / 02:00", false); }), _captureCancellation.Token);
            _timer.Stop(); _captureClock?.Stop();

            var finalContext = _raidLogs.Read(tarkov.StartTime);
            CaptureValidation.EnsureComplete(metrics, finalContext, HasExited(tarkov));
            SetStatus("Measurement complete. Return to the benchmark window.", false);
            await WindowAttention.NotifyAsync(this);

            var contextDialog = new ContextWindow(finalContext) { Owner = this };
            if (contextDialog.ShowDialog() != true || contextDialog.Answers is null) { SetStatus("Measurement completed but was not saved.", true); return; }
            var answers = contextDialog.Answers; var warnings = settingsTask.Result.Warnings.Concat(systemTask.Result.Warnings).Distinct().ToList();
            var run = new BenchmarkRun(Guid.NewGuid().ToString(), DateTime.Now.ToString("yyyy-MM-dd"), CaptureDurationSeconds, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0", systemTask.Result.System, settingsTask.Result.Settings, new { map = answers.Map, execution = answers.Execution, weather = answers.Weather, time_of_day = answers.TimeOfDay, game_version = finalContext.GameVersion }, metrics, warnings);
            _store.Append(run); LoadLatestResult(); SetStatus("Benchmark saved locally. No data was uploaded.", false); StatusDetailText.Text = $"{answers.Map} · {metrics.SampleCount:N0} frames · {warnings.Count} warning(s)";
            var result = new CommandResult("completed", run.RunId, answers.Map, metrics.AverageFps, metrics.OnePercentLowFps, metrics.ZeroPointOnePercentLowFps, metrics.P95FrametimeMs, true, false); NativeConsole.WriteResult(result); _completedCommand = true;
            if (_invocation.SourceSkill) { await Task.Delay(2000); Close(); }
        }
        catch (OperationCanceledException) { SetStatus("Collection canceled. Partial data was discarded.", true); WriteTerminalResult("cancelled", "Collection canceled by the user."); }
        catch (IncompleteCaptureException ex) { SetStatus("Measurement discarded. No benchmark data was saved.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("discarded", ex.Message); }
        catch (Exception) when (HasExited(tarkov)) { const string message = "Tarkov closed before the measurement completed. The partial result was discarded."; SetStatus("Measurement discarded. No benchmark data was saved.", true); StatusDetailText.Text = message; WriteTerminalResult("discarded", message); }
        catch (PresentMonPermissionException ex) { SetStatus("PresentMon needs permission to access Windows performance tracing.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("permission_required", ex.Message); }
        catch (PresentMonSessionException ex) { ShowCollectionUnavailable(ex.Message); WriteTerminalResult("capture_conflict", ex.Message); }
        catch (Exception ex) { SetStatus("Measurement failed. No benchmark data was saved.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("failed", ex.Message); }
        finally { _timer.Stop(); _captureClock = null; _captureCancellation?.Dispose(); _captureCancellation = null; SetCollecting(false); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { CancelButton.IsEnabled = false; SetStatus("Stopping collection. This run will be discarded.", true); _captureCancellation?.Cancel(); }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { AppPaths.EnsureDataDirectory(); Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true }); }
    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = _store.Load();
            var runs = BenchmarkSubmission.SelectUnsubmitted(document.Runs).ToList();
            var markSubmitted = runs.Count > 0;
            if (runs.Count == 0)
            {
                var copyAll = MessageBox.Show(this, $"All {document.Runs.Count} saved run(s) are already marked as submitted. Copy the most recent {Math.Min(document.Runs.Count, BenchmarkSubmission.MaxRuns)} again?", "Nothing new to submit", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (copyAll != MessageBoxResult.Yes) return;
                runs = BenchmarkSubmission.SelectMostRecent(document.Runs).ToList();
            }

            Clipboard.SetText(BenchmarkSubmission.Serialize(runs));
            var submissionWindow = new SubmissionWindow(runs.Count) { Owner = this };
            if (submissionWindow.ShowDialog() != true) return;

            _pendingSubmissionRunIds = markSubmitted ? runs.Select(run => run.RunId).ToList() : [];
            _waitingForSubmissionReturn = markSubmitted;
            _submissionWindowLostFocus = false;
            Process.Start(new ProcessStartInfo(PerformanceFormUrl) { UseShellExecute = true });
            SetStatus($"Copied {runs.Count} run(s) to the clipboard. Paste the JSON into the form.", false);
            StatusDetailText.Text = "The form was opened in your browser. Nothing was uploaded automatically.";
        }
        catch (Exception ex)
        {
            ResetPendingSubmission();
            SetStatus("Benchmark submission could not be prepared.", true);
            StatusDetailText.Text = ex.Message;
        }
    }
    private void SubmissionWindow_Deactivated(object? sender, EventArgs e) { if (_waitingForSubmissionReturn) _submissionWindowLostFocus = true; }
    private void SubmissionWindow_Activated(object? sender, EventArgs e)
    {
        if (!_waitingForSubmissionReturn || !_submissionWindowLostFocus) return;
        _waitingForSubmissionReturn = false;
        Dispatcher.BeginInvoke(ConfirmSubmissionAfterBrowserReturn);
    }
    private void ConfirmSubmissionAfterBrowserReturn()
    {
        var runIds = _pendingSubmissionRunIds;
        ResetPendingSubmission();
        if (runIds.Count == 0) return;
        var submitted = MessageBox.Show(this, "Did you paste the JSON and submit the form?", "Confirm submission", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (submitted != MessageBoxResult.Yes) return;
        _store.MarkSubmitted(runIds);
        SetStatus($"Marked {runIds.Count} run(s) as submitted.", false);
    }
    private void ResetPendingSubmission() { _waitingForSubmissionReturn = false; _submissionWindowLostFocus = false; _pendingSubmissionRunIds = []; }
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (_captureCancellation is not null) { _captureCancellation.Cancel(); } if (_invocation.SourceSkill && !_completedCommand) WriteTerminalResult("cancelled", "The benchmark window was closed."); }
    private void SetCollecting(bool collecting) { StartButton.IsEnabled = !collecting && File.Exists(AppPaths.PresentMonFile); CancelButton.IsEnabled = collecting; }
    private void SetStatus(string text, bool warning) { StatusText.Text = text; StatusText.Foreground = (Brush)FindResource(warning ? "WarningBrush" : "TextBrush"); }
    private void ShowInfo(string message) { SetStatus(message, true); MessageBox.Show(this, message, "Ready when you are", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void ShowCollectionUnavailable(string message) { SetStatus("Collection could not start.", true); StatusDetailText.Text = message; MessageBox.Show(this, message, "Collection unavailable", MessageBoxButton.OK, MessageBoxImage.Warning); }
    private void DisplayMetrics(PerformanceMetrics metrics) { AverageFpsText.Text = metrics.AverageFps.ToString("0.0"); OneLowText.Text = metrics.OnePercentLowFps.ToString("0.0"); PointOneLowText.Text = metrics.ZeroPointOnePercentLowFps.ToString("0.0"); P95Text.Text = $"{metrics.P95FrametimeMs:0.0} ms"; }
    private void UpdateRunCount(int count) { LatestResultTitle.Text = count switch { 0 => "LATEST RESULT · NO RUNS", 1 => "LATEST RESULT · 1 RUN", _ => $"LATEST RESULT · {count} RUNS" }; }
    private static bool HasExited(Process process) { try { return process.HasExited; } catch { return true; } }
    private void WriteTerminalResult(string status, string message) { if (_completedCommand) return; NativeConsole.WriteResult(new CommandResult(status, Message: message)); _completedCommand = true; }
}

internal sealed class IncompleteCaptureException(string message) : Exception(message);

internal static class CaptureValidation
{
    public static void EnsureComplete(PerformanceMetrics metrics, RaidContext context, bool tarkovExited)
    {
        if (tarkovExited) throw new IncompleteCaptureException("Tarkov closed before the measurement completed. The partial result was discarded.");
        if (!context.Active) throw new IncompleteCaptureException("The raid ended before the measurement completed. The partial result was discarded.");
        if (metrics.DurationSec < 110) throw new IncompleteCaptureException($"Only {metrics.DurationSec:0.0} seconds of valid frametime data were captured. The partial result was discarded.");
    }
}
