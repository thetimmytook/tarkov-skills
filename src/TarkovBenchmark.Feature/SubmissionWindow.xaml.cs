using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TarkovBenchmark.Feature.Authentication;
using TarkovSkills.Core.Authentication;

namespace TarkovBenchmark.Feature;

public partial class SubmissionWindow : Window
{
    private readonly AccountController account;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool closing;
    private bool mayClose;
    private bool accepted;

    public SubmissionWindow(int runCount, DesktopAuthProduct product)
    {
        InitializeComponent();
        SummaryText.Text = $"JSON for {runCount} run(s) is ready.";
        account = AccountController.Create(product, Path.Combine(AppContext.BaseDirectory, "desktop-auth.json"));
        DataContext = account;
        AccountRoot.Content = new AccountView(account);
        Loaded += OnLoaded;
        Closing += OnClosing;
        timer.Tick += async (_, _) => await account.RefreshIfNeededAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await account.EnsureSignedInAsync();
        if (!closing) timer.Start();
    }

    private async void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (!account.CanContinue || closing) return;
        // The other app may have signed out since our last periodic check.
        await account.RestoreAsync();
        if (!account.CanContinue || closing) return;
        accepted = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (mayClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        timer.Stop();
        await account.StopAsync();
        // Let WPF finish the first Closing event before completing the dialog.
        await Dispatcher.InvokeAsync(() =>
        {
            mayClose = true;
            DialogResult = accepted;
        });
    }
}
