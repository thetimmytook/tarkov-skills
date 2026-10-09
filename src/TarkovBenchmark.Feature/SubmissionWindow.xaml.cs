using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using TarkovBenchmark.Feature.Authentication;
using TarkovSkills.Core;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Authentication;

namespace TarkovBenchmark.Feature;

public partial class SubmissionWindow : Window
{
    private readonly AccountController account;
    private readonly AcademyApiClient? api;
    private readonly SubmissionOutbox? outbox;
    private readonly SubmissionWorkflow? workflow;
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private PreparedSubmission? prepared;
    private Task active = Task.CompletedTask;
    private bool sending, completed, closing, mayClose;

    public SubmissionWindow(IReadOnlyList<BenchmarkRun> runs, DesktopAuthProduct product)
    {
        InitializeComponent();
        account = AccountController.Create(product, Path.Combine(AppContext.BaseDirectory, "desktop-auth.json"));
        AccountRoot.Content = new AccountView(account);
        try
        {
            var configuration = AcademyConfiguration.Read(Path.Combine(AppContext.BaseDirectory, "academy-api.json"));
            var auth = AuthConfiguration.Read(Path.Combine(AppContext.BaseDirectory, "desktop-auth.json"), product);
            if (account.DesktopSession is not null)
            {
                api = new AcademyApiClient(configuration.BaseUri, account.DesktopSession, configuration.AllowLoopbackHttp);
                outbox = new SubmissionOutbox(AppPaths.DataDirectory, configuration.BaseUri, auth.Issuer, auth.ClientId);
                workflow = new SubmissionWorkflow(api, outbox);
            }
            DestinationText.Text = $"Destination: {configuration.BaseUri.Host}";
        }
        catch { ShowResult("Error", "Academy submission is not configured for this build. Local tools remain available.", "Error"); }
        account.PropertyChanged += AccountChanged;
        RunCombo.ItemsSource = runs.Reverse().Select((run, index) => new RunChoice(run, index + 1)).ToList();
        RunCombo.SelectedIndex = runs.Count > 0 ? 0 : -1;
        Loaded += OnLoaded;
        Closing += OnClosing;
        timer.Tick += async (_, _) => { if (!sending) await account.RefreshIfNeededAsync(); };
        UpdateButton();
    }

    private sealed record RunChoice(BenchmarkRun Run, int Index)
    {
        public override string ToString() => $"{Index}. {Run.CollectedDate} · {Run.Performance.AverageFps:0.0} FPS";
    }

    private void AccountChanged(object? sender, PropertyChangedEventArgs e) => UpdateButton();
    private void UpdateButton()
    {
        SendButton.IsEnabled = !closing && !sending && !completed && workflow is not null && account.CanContinue && GpuCombo.SelectedItem is string;
        CheckButton.IsEnabled = !closing && !sending && workflow is not null && account.CanContinue && RunCombo.SelectedItem is RunChoice;
    }
    private void Run_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (GpuCombo is null || RunCombo.SelectedItem is not RunChoice choice) return;
        try
        {
            var names = SubmissionPayload.GpuNames(choice.Run);
            GpuCombo.ItemsSource = names;
            GpuCombo.SelectedIndex = names.Count == 1 ? 0 : -1;
            SummaryText.Text = "Choose the GPU used for this run, then review the submission.";
        }
        catch { GpuCombo.ItemsSource = null; SummaryText.Text = "This run has incomplete hardware information."; }
        completed = false;
        if (outbox is not null && Guid.TryParse(choice.Run.RunId, out var id))
        {
            try
            {
                var saved = outbox.ReadStatus(id);
                ShowResult("Saved status", saved is null ? "No server status saved for this run." : LastConfirmed(saved));
                completed = saved?.Status == "deleted";
            }
            catch { ShowResult("Error", "Saved status could not be read. Sending is blocked until storage is available.", "Error"); }
        }
        UpdateButton();
    }
    private void Gpu_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (account is not null) UpdateButton();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (api is not null) await account.EnsureSignedInAsync();
        if (!closing) timer.Start();
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (!SendButton.IsEnabled || api is null || outbox is null) return;
        if (prepared is null)
        {
            try
            {
                prepared = outbox.Prepare(((RunChoice)RunCombo.SelectedItem).Run, (string)GpuCombo.SelectedItem);
                DisplayPrepared(prepared);
                ShowResult("Ready to send", "Only this run will be sent. It becomes public only after approval. No raw captures or private device identifiers are included.");
            }
            catch (InvalidDataException ex) { ShowResult("Error", ex.Message, "Error"); }
            catch { ShowResult("Error", "Submission could not be prepared or saved. Nothing was sent.", "Error"); }
            return;
        }
        await StartOperationAsync(() => workflow!.SubmitAsync(prepared!, lifetime.Token));
    }

    private void DisplayPrepared(PreparedSubmission submission)
    {
        SummaryText.Text = submission.Summary;
        ResourceReview.Show(submission.ResourceTelemetry);
        ResourceReview.Visibility = Visibility.Visible;
        RunCombo.IsEnabled = GpuCombo.IsEnabled = false;
        SendButton.Content = "Send for review";
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckButton.IsEnabled || RunCombo.SelectedItem is not RunChoice choice ||
            !Guid.TryParse(choice.Run.RunId, out var id)) return;
        await StartOperationAsync(() => workflow!.CheckAsync(id, lifetime.Token));
    }

    private Task StartOperationAsync(Func<Task<SubmissionCheck>> operation)
    {
        sending = true;
        AccountRoot.IsEnabled = false;
        RunCombo.IsEnabled = GpuCombo.IsEnabled = false;
        timer.Stop();
        UpdateButton();
        ShowResult("Please wait", "Checking with Academy…");
        active = CompleteOperationAsync(operation);
        return active;
    }

    private async Task CompleteOperationAsync(Func<Task<SubmissionCheck>> operation)
    {
        try
        {
            var result = await operation();
            if (closing) return;
            if (result.Checkpoint is { } checkpoint)
            {
                completed = true;
                ShowCheckpoint(checkpoint);
                if (result.Outcome == SubmissionOutcome.StorageUnavailable)
                    ShowResult("Error", LastConfirmed(checkpoint) + " The server reply could not be saved locally. Check status again later.", "Error");
            }
            else
            {
                var message = result.Outcome switch
                {
                    SubmissionOutcome.NotFound => "No submission was found for the current account. This does not prove the run was never submitted by another account.",
                    SubmissionOutcome.Conflict => "Academy reports a conflict with an earlier submission. No new ID will be created. Check status before continuing.",
                    SubmissionOutcome.StorageUnavailable => "Saved submission status could not be read or written. Check storage and retry; no success is assumed.",
                    _ => "Current status is not confirmed. Check your connection and sign-in, then choose Check status. No automatic resend will occur."
                };
                ShowResult(result.Outcome == SubmissionOutcome.NotFound ? "Not found" : "Error", message, result.Outcome == SubmissionOutcome.NotFound ? "Info" : "Error");
                if (result.Outcome == SubmissionOutcome.Conflict) completed = true;
                if (prepared is not null) SendButton.Content = "Retry same submission";
                // Keep the previous observation visible, explicitly dated rather than current.
                try
                {
                    var id = prepared?.ClientRunId ?? Guid.Parse(((RunChoice)RunCombo.SelectedItem).Run.RunId);
                    if (outbox?.ReadStatus(id) is { } saved) ResultText.Text += "\n" + LastConfirmed(saved);
                }
                catch { /* Status storage errors are already surfaced without filesystem details. */ }
            }
        }
        catch { if (!closing) ShowResult("Error", "Current status is not confirmed. Choose Check status later. No automatic resend will occur.", "Error"); }
        finally
        {
            sending = false;
            if (!closing)
            {
                AccountRoot.IsEnabled = true;
                RunCombo.IsEnabled = GpuCombo.IsEnabled = prepared is null;
                await account.RestoreAsync();
                timer.Start();
                UpdateButton();
            }
        }
    }

    private void ShowResult(string title, string detail, string tone = "Info")
    {
        ResultTitle.Text = title;
        ResultText.Text = detail;
        ResultBanner.Background = (Brush)FindResource(tone + "BackgroundBrush");
        ResultBanner.BorderBrush = (Brush)FindResource(tone + "BorderBrush");
        ResultTitle.Foreground = (Brush)FindResource(tone == "Success" ? "ReadyBrush" : tone + "Brush");
    }

    private void ShowCheckpoint(PublicationCheckpoint checkpoint)
    {
        var (title, detail, tone) = checkpoint.Status switch
        {
            "pending_review" => ("Confirmed", "Pending review — not public yet. Academy has received this run and it is waiting for moderation.", "Success"),
            "published" => ("Confirmed", "Published — this run is public on Academy.", "Success"),
            "rejected" => ("Rejected", "This run was not approved and is not public.", "Warning"),
            "deleted" => ("Deleted", "This publication was deleted. The run will not be sent again automatically.", "Info"),
            _ => ("Error", "The server status could not be confirmed.", "Error")
        };
        ShowResult(title, detail + $"\nChecked {checkpoint.CheckedAt.LocalDateTime:g}.", tone);
    }

    private static string LastConfirmed(PublicationCheckpoint checkpoint)
    {
        var label = checkpoint.Status switch
        {
            "pending_review" => "Pending review · Not public yet",
            "published" => "Published · Public on Academy",
            "rejected" => "Rejected · Not public",
            "deleted" => "Deleted · Will not be sent again",
            _ => "Unknown"
        };
        return $"Last confirmed: {label}. Checked {checkpoint.CheckedAt.LocalDateTime:g}.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (mayClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        timer.Stop();
        lifetime.Cancel();
        await active;
        await account.StopAsync();
        api?.Dispose();
        account.PropertyChanged -= AccountChanged;
        lifetime.Dispose();
        await Dispatcher.InvokeAsync(() => { mayClose = true; DialogResult = completed; });
    }
}
