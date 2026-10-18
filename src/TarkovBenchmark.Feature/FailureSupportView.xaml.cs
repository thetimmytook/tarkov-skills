using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TarkovSkills.Core;
using TarkovSkills.Core.Authentication;

namespace TarkovBenchmark.Feature;

public partial class FailureSupportView : UserControl
{
    private readonly Action<string> _copy;
    private readonly Action<Uri> _openIssue;
    private readonly Action<string> _showArchive;
    internal SupportBundle? Bundle { get; private set; }

    public FailureSupportView() : this(Clipboard.SetText, uri => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }),
        path =>
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add("/select,"); start.ArgumentList.Add(path);
            Process.Start(start);
        }) { }

    internal FailureSupportView(Action<string> copy, Action<Uri> openIssue, Action<string> showArchive)
    { InitializeComponent(); _copy = copy; _openIssue = openIssue; _showArchive = showArchive; }

    public void ShowFailure(DesktopAuthProduct product, string version, SupportOperation operation, Exception error) =>
        ShowBundle(SupportArchive.Create(product, version, operation, error));

    public void ShowBundle(SupportBundle bundle)
    {
        Bundle = bundle;
        var copied = false;
        try { _copy(bundle.Report); copied = true; } catch { }
        ArchiveButton.IsEnabled = bundle.ArchivePath is not null;
        ArchiveButton.ToolTip = bundle.ArchivePath is null ? null : Path.GetFileName(bundle.ArchivePath);
        SupportStatusText.Text = bundle.ArchivePath is null
            ? "The log archive could not be created. Open an issue with the sanitized report."
            : "The ZIP is saved locally. Review it, then attach it to the GitHub issue manually. Issues are public.";
        if (copied) SupportStatusText.Text += " Report copied.";
        Visibility = Visibility.Visible;
    }

    public void Clear() { Bundle = null; Visibility = Visibility.Collapsed; }

    private void Issue_Click(object sender, RoutedEventArgs e)
    {
        if (Bundle is null) return;
        try { _openIssue(Bundle.IssueUri); }
        catch { SupportStatusText.Text = "The browser could not be opened. Report the problem at github.com/thetimmytook/tarkov-skills/issues."; }
    }
    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (Bundle?.ArchivePath is not { } path) return;
        try { _showArchive(path); }
        catch { SupportStatusText.Text = "The archive folder could not be opened. Find the support ZIP in Settings → Open diagnostic folder."; }
    }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Clear();
}
