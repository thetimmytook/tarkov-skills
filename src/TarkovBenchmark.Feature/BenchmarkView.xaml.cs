using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TarkovSkills.Core;

namespace TarkovBenchmark.Feature;

public partial class BenchmarkView : UserControl
{
    private const int CaptureDurationSeconds = 120;
    private const string PerformanceFormUrl = "https://forms.gle/D692T2Umd5ktD5wj8";
    private readonly BenchmarkFeatureOptions _options;
    private readonly PresentMonRunner _presentMon = new();
    private readonly RaidLogReader _raidLogs = new();
    private readonly BenchmarkStore _store = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? _captureCancellation;
    private Stopwatch? _captureClock;
    private bool _completedCommand;
    private bool _waitingForSubmissionReturn;
    private bool _submissionWindowLostFocus;
    private IReadOnlyList<string> _pendingSubmissionRunIds = [];
    private Window? _owner;

    public event EventHandler? RequestClose;

    public BenchmarkView() : this(new BenchmarkFeatureOptions("1.0.0")) { }

    public BenchmarkView(BenchmarkFeatureOptions options)
    {
        InitializeComponent();
        _options = options;
        CopyResultsButton.Visibility = options.ShowCopyResults ? Visibility.Visible : Visibility.Collapsed;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _timer.Tick += (_, _) =>
        {
            if (_captureClock is not null)
                SetStatus($"Collecting frametime data: {_captureClock.Elapsed:mm\\:ss} / 02:00", false);
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _owner = Window.GetWindow(this);
        if (_owner is not null)
        {
            _owner.Activated += Owner_Activated;
            _owner.Deactivated += Owner_Deactivated;
        }
        RefreshDependency();
        LoadLatestResult();
        if (_options.CollectRequested) SetStatus("Ready for a skill-requested benchmark. Start the raid, then press Start collection.", false);
        else if (TarkovState.FindProcess() is null) SetStatus("Run Tarkov and enter a raid to collect performance data.", true);
        else SetStatus("Tarkov is running. Enter a raid, then press Start collection.", false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_owner is not null)
        {
            _owner.Activated -= Owner_Activated;
            _owner.Deactivated -= Owner_Deactivated;
        }
        _captureCancellation?.Cancel();
        if (_options.SourceSkill && !_completedCommand)
            WriteTerminalResult("cancelled", "The benchmark window was closed.");
    }

    private void RefreshDependency()
    {
        var ready = _presentMon.IsDependencyReady(out var message);
        PresentMonText.Text = message;
        PresentMonDot.Fill = (Brush)FindResource(ready ? "ReadyBrush" : "ErrorBrush");
        StartButton.IsEnabled = ready;
        if (!ready) SetStatus("The bundled performance collector failed its integrity check.", true);
    }

    private void LoadLatestResult()
    {
        OpenFolderButton.IsEnabled = File.Exists(AppPaths.BenchmarkFile);
        try
        {
            var document = _store.Load();
            UpdateRunCount(document.Runs.Count);
            CopyResultsButton.IsEnabled = document.Runs.Count > 0;
            SubmitButton.IsEnabled = document.Runs.Count > 0;
            var latest = document.Runs.LastOrDefault();
            if (latest is not null) DisplayMetrics(latest.Performance);
        }
        catch
        {
            SubmitButton.IsEnabled = false;
            CopyResultsButton.IsEnabled = false;
            UpdateRunCount(0);
            StatusDetailText.Text = "Existing benchmark data uses an unsupported prototype format.";
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        using var tarkov = TarkovState.FindProcess();
        if (tarkov is null) { ShowInfo("Run Tarkov and enter a raid, then try again."); return; }
        RaidContext initialContext;
        try { initialContext = _raidLogs.Read(tarkov.StartTime); }
        catch (Exception ex) { ShowInfo($"Tarkov logs could not be read: {ex.Message}"); return; }
        if (!initialContext.Active) { ShowInfo("Now start the raid, then try again."); return; }

        _completedCommand = false;
        SetCollecting(true);
        SetStatus("Preparing benchmark...", false);
        _captureCancellation = new CancellationTokenSource();
        try
        {
            var settingsTask = Task.Run(() => new SettingsReader().Read());
            var systemTask = Task.Run(() => new SystemInfoCollector().Collect());
            await Task.WhenAll(settingsTask, systemTask);
            var metrics = await new FrametimeCaptureService().CaptureAsync(CaptureDurationSeconds, () => Dispatcher.Invoke(() =>
            {
                _captureClock = Stopwatch.StartNew();
                _timer.Start();
                SetStatus("Collecting frametime data: 00:00 / 02:00", false);
            }), _captureCancellation.Token);

            _timer.Stop();
            _captureClock?.Stop();
            var finalContext = _raidLogs.Read(tarkov.StartTime);
            SetStatus("Measurement complete. Return to the benchmark window.", false);
            await WindowAttention.NotifyAsync(_owner ?? Window.GetWindow(this));

            var contextDialog = new ContextWindow(finalContext) { Owner = _owner };
            if (contextDialog.ShowDialog() != true || contextDialog.Answers is null)
            {
                SetStatus("Measurement completed but was not saved.", true);
                return;
            }

            var answers = contextDialog.Answers;
            var warnings = settingsTask.Result.Warnings.Concat(systemTask.Result.Warnings).Distinct().ToList();
            var run = new BenchmarkRun(Guid.NewGuid().ToString(), DateTime.Now.ToString("yyyy-MM-dd"), CaptureDurationSeconds, _options.ApplicationVersion, systemTask.Result.System, settingsTask.Result.Settings, new { map = answers.Map, execution = answers.Execution, weather = answers.Weather, time_of_day = answers.TimeOfDay, game_version = finalContext.GameVersion }, metrics.Performance, warnings);
            _store.Append(run);
            LoadLatestResult();
            SetStatus("Benchmark saved locally. No data was uploaded.", false);
            StatusDetailText.Text = $"{answers.Map} · {metrics.Performance.SampleCount:N0} frames · {warnings.Count} warning(s)";
            WriteResult(new CommandResult("completed", run.RunId, answers.Map, metrics.Performance.AverageFps, metrics.Performance.OnePercentLowFps, metrics.Performance.ZeroPointOnePercentLowFps, metrics.Performance.P95FrametimeMs, true, false));
            if (_options.SourceSkill) { await Task.Delay(2000); RequestClose?.Invoke(this, EventArgs.Empty); }
        }
        catch (CaptureDiscardedException ex) { SetStatus("Measurement discarded. No benchmark data was saved.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("discarded", ex.Message); }
        catch (OperationCanceledException) { SetStatus("Collection canceled. Partial data was discarded.", true); WriteTerminalResult("cancelled", "Collection canceled by the user."); }
        catch (PresentMonPermissionException ex) { SetStatus("PresentMon needs permission to access Windows performance tracing.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("permission_required", ex.Message); }
        catch (PresentMonSessionException ex) { ShowCollectionUnavailable(ex.Message); WriteTerminalResult("capture_conflict", ex.Message); }
        catch (Exception) when (HasExited(tarkov)) { const string message = "Tarkov closed before the measurement completed. The partial result was discarded."; SetStatus("Measurement discarded. No benchmark data was saved.", true); StatusDetailText.Text = message; WriteTerminalResult("discarded", message); }
        catch (Exception ex) { SetStatus("Measurement failed. No benchmark data was saved.", true); StatusDetailText.Text = ex.Message; WriteTerminalResult("failed", ex.Message); }
        finally
        {
            _timer.Stop();
            _captureClock = null;
            _captureCancellation?.Dispose();
            _captureCancellation = null;
            SetCollecting(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _captureClock?.Stop();
        CancelButton.IsEnabled = false;
        SetStatus("Stopping collection. This run will be discarded.", true);
        _captureCancellation?.Cancel();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureDataDirectory();
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true });
    }

    private void CopyResults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(BenchmarkSubmission.SerializeLatest(_store.Load().Runs));
            SetStatus("Latest benchmark result copied to the clipboard.", false);
            StatusDetailText.Text = "Paste the JSON into your web conversation for analysis. Nothing was uploaded.";
        }
        catch (Exception ex)
        {
            SetStatus("Benchmark result could not be copied.", true);
            StatusDetailText.Text = ex.Message;
        }
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = _store.Load();
            var runs = BenchmarkSubmission.SelectUnsubmitted(document.Runs).ToList();
            var markSubmitted = runs.Count > 0;
            if (runs.Count == 0)
            {
                var copyAll = MessageBox.Show(_owner, $"All {document.Runs.Count} saved run(s) are already marked as submitted. Copy the most recent {Math.Min(document.Runs.Count, BenchmarkSubmission.MaxRuns)} again?", "Nothing new to submit", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (copyAll != MessageBoxResult.Yes) return;
                runs = BenchmarkSubmission.SelectMostRecent(document.Runs).ToList();
            }

            Clipboard.SetText(BenchmarkSubmission.Serialize(runs));
            var submissionWindow = new SubmissionWindow(runs.Count) { Owner = _owner };
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

    private void Owner_Deactivated(object? sender, EventArgs e) { if (_waitingForSubmissionReturn) _submissionWindowLostFocus = true; }
    private void Owner_Activated(object? sender, EventArgs e)
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
        var submitted = MessageBox.Show(_owner, "Did you paste the JSON and submit the form?", "Confirm submission", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (submitted != MessageBoxResult.Yes) return;
        _store.MarkSubmitted(runIds);
        SetStatus($"Marked {runIds.Count} run(s) as submitted.", false);
    }

    private void ResetPendingSubmission() { _waitingForSubmissionReturn = false; _submissionWindowLostFocus = false; _pendingSubmissionRunIds = []; }
    private void SetCollecting(bool collecting)
    {
        StartButton.IsEnabled = !collecting && _presentMon.IsDependencyReady(out _);
        CancelButton.IsEnabled = collecting;
    }
    private void SetStatus(string text, bool warning) { StatusText.Text = text; StatusText.Foreground = (Brush)FindResource(warning ? "WarningBrush" : "TextBrush"); }
    private void ShowInfo(string message) { SetStatus(message, true); MessageBox.Show(_owner, message, "Ready when you are", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void ShowCollectionUnavailable(string message) { SetStatus("Collection could not start.", true); StatusDetailText.Text = message; MessageBox.Show(_owner, message, "Collection unavailable", MessageBoxButton.OK, MessageBoxImage.Warning); }
    private void DisplayMetrics(PerformanceMetrics metrics) { AverageFpsText.Text = metrics.AverageFps.ToString("0.0"); OneLowText.Text = metrics.OnePercentLowFps.ToString("0.0"); PointOneLowText.Text = metrics.ZeroPointOnePercentLowFps.ToString("0.0"); P95Text.Text = $"{metrics.P95FrametimeMs:0.0} ms"; }
    private void UpdateRunCount(int count) { LatestResultTitle.Text = count switch { 0 => "LATEST RESULT · NO RUNS", 1 => "LATEST RESULT · 1 RUN", _ => $"LATEST RESULT · {count} RUNS" }; }
    private void WriteResult(CommandResult result) { _options.ResultWriter?.Invoke(result); _completedCommand = true; }
    private void WriteTerminalResult(string status, string message) { if (_completedCommand) return; WriteResult(new CommandResult(status, Message: message)); }
    private static bool HasExited(Process process) { try { return process.HasExited; } catch { return true; } }
}
