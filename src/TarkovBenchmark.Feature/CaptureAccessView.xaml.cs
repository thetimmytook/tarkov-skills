using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TarkovSkills.Core;
using TarkovSkills.Core.Permissions;

namespace TarkovBenchmark.Feature;

public partial class CaptureAccessView : UserControl
{
    private readonly Func<EtwPermissionState> _check;
    private readonly Func<EtwPermissionAction, CancellationToken, Task<EtwPermissionResult>> _change;
    private readonly Func<EtwPermissionAction, bool> _confirm;
    private readonly Func<bool> _temporaryActive;
    private readonly CaptureAccessSession? _session;
    private CancellationTokenSource? _operationCancellation;

    public CaptureAccessView(CaptureAccessSession session) : this(EtwPermissions.CheckCurrent, async (action, cancellation) =>
    {
        if (action == EtwPermissionAction.Add) return await session.EnableAsync(cancellation);
        session.StopTemporaryCollector();
        return await EtwPermissions.RequestChangeAsync(action, cancellation);
    }, null, () => session.TemporaryCollectorActive)
    {
        _session = session;
        DiagnosticProbePanel.Visibility = CaptureDiagnostics.PermissionProbeEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    internal CaptureAccessView(Func<EtwPermissionState> check,
        Func<EtwPermissionAction, CancellationToken, Task<EtwPermissionResult>> change,
        Func<EtwPermissionAction, bool>? confirm, Func<bool>? temporaryActive = null)
    {
        InitializeComponent();
        _check = check; _change = change; _confirm = confirm ?? Confirm;
        _temporaryActive = temporaryActive ?? (() => false);
        Unloaded += (_, _) => CancelPendingOperations();
        Loaded += (_, _) => { if (_operationCancellation is null) RefreshAccess(); };
        RefreshAccess();
    }

    internal void CancelPendingOperations() => _operationCancellation?.Cancel();

    private bool Confirm(EtwPermissionAction action)
    {
        var message = action == EtwPermissionAction.Add
            ? "Allow administrator setup for this Windows account? Setup adds it to Performance Log Users, granting tracing permission to other apps too. It remains after uninstall. A temporary elevated collector allows capture immediately, without signing out. Ordinary access takes effect at your next Windows sign-in."
            : "Stop this application's temporary elevated collector and remove this Windows account's direct membership in Performance Log Users? Other performance tools may lose access after your next Windows sign-in. Existing access through another group will remain.";
        return MessageBox.Show(Window.GetWindow(this), message, "Change Windows capture access", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private void RefreshAccess(string? result = null)
    {
        var working = _operationCancellation is not null;
        try
        {
            var state = _check();
            var temporary = _temporaryActive();
            var status = temporary ? "Ready to capture now using a temporary collector." : state switch
            {
                { CurrentTokenHasAccess: true } => "Windows tracing permission is available. Capture will verify access.",
                { DirectMembership: true } => "Account configured. Set up access to capture now, or sign in to Windows again.",
                { DirectMembership: false } => "Capture access needs administrator setup.",
                _ => "Capture access could not be fully checked. Try Check access."
            };
            AccessStatusText.Text = temporary || result is null ? status : result + "\n\n" + status;
            AddAccessButton.IsEnabled = !working && !state.CurrentTokenHasAccess && !temporary;
            RemoveAccessButton.IsEnabled = !working && state.DirectMembership == true;
        }
        catch (Exception)
        {
            AccessStatusText.Text = result ?? "Windows capture access could not be checked. No account settings were changed.";
            AddAccessButton.IsEnabled = false; RemoveAccessButton.IsEnabled = false;
        }
        RefreshAccessButton.IsEnabled = !working;
        ProbeButton.IsEnabled = !working;
    }

    private async Task ChangeAsync(EtwPermissionAction action)
    {
        if (_operationCancellation is not null || !_confirm(action)) return;
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        RefreshAccess("Waiting for Windows administrator approval…");
        string? message = null;
        try
        {
            var result = await _change(action, cancellation.Token);
            EtwPermissionReport.Save(action, result);
            message = result.Message;
        }
        catch (OperationCanceledException) { message = "Permission setup cancelled. Check access before trying again."; }
        catch (Exception)
        {
            EtwPermissionReport.Save(action, new(EtwPermissionOutcome.Unconfirmed));
            message = "The permission setup result could not be confirmed. Check access before trying again.";
        }
        finally
        {
            _operationCancellation = null;
            RefreshAccess(message);
        }
    }

    private async void Probe_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCancellation is not null || !CaptureDiagnostics.PermissionProbeEnabled || _session is null) return;
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        RefreshAccess();
        ProbeCancelButton.IsEnabled = true;
        ProbeStatusText.Text = "Checking Windows tracing. No game or Windows sign-out is needed.";
        try
        {
            await _session.TestCaptureAccessAsync(async token => await Dispatcher.InvokeAsync(() =>
                !token.IsCancellationRequested && CaptureAccessConsent.Show(Window.GetWindow(this))), cancellation.Token);
            ProbeStatusText.Text = _session.TemporaryCollectorActive
                ? "Passed using the temporary elevated collector, without signing out. Repeat the test to check reuse. Logs: Open diagnostic folder above."
                : "Passed with ordinary Windows rights. Logs: Open diagnostic folder above.";
        }
        catch (OperationCanceledException) { ProbeStatusText.Text = "Test cancelled. No benchmark data was saved."; }
        catch (Exception) { ProbeStatusText.Text = "The permission test failed. Open the diagnostic folder and share the latest permission-probe log."; }
        finally
        {
            _operationCancellation = null;
            ProbeCancelButton.IsEnabled = false;
            RefreshAccess();
        }
    }

    private async void AddAccess_Click(object sender, RoutedEventArgs e) => await ChangeAsync(EtwPermissionAction.Add);
    private async void RemoveAccess_Click(object sender, RoutedEventArgs e) => await ChangeAsync(EtwPermissionAction.Remove);
    private void RefreshAccess_Click(object sender, RoutedEventArgs e) => RefreshAccess();
    private void ProbeCancel_Click(object sender, RoutedEventArgs e) => CancelPendingOperations();
    private void OpenReports_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ReportsDirectory);
            var open = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            open.ArgumentList.Add(AppPaths.ReportsDirectory);
            Process.Start(open);
        }
        catch (Exception) { AccessStatusText.Text = "The local diagnostic folder could not be opened."; }
    }
}
