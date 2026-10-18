using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class FailureSupportUiTests(WpfHost host)
{
    [Fact]
    public void FailureOffersLocalArchiveAndIssueWithoutOpeningAnythingUntilClicked() => host.Run(() =>
    {
        var copies = new List<string>(); var issues = new List<Uri>(); var archives = new List<string>();
        var view = new FailureSupportView(copies.Add, issues.Add, archives.Add);
        var bundle = Bundle(true);
        view.ShowBundle(bundle);
        Assert.Equal(new[] { bundle.Report }, copies);
        Assert.Empty(issues); Assert.Empty(archives);
        Assert.Equal(Visibility.Visible, view.Visibility);
        Assert.Contains("manually", ((TextBlock)view.FindName("SupportStatusText")).Text);
        ((Button)view.FindName("IssueButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((Button)view.FindName("ArchiveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new[] { bundle.IssueUri }, issues); Assert.Equal(new[] { bundle.ArchivePath }, archives);
        view.Clear(); Assert.Equal(Visibility.Collapsed, view.Visibility);
    });

    [Fact]
    public void ClipboardAndStorageFailuresLeaveIssueActionUsable() => host.Run(() =>
    {
        var opened = false;
        var view = new FailureSupportView(_ => throw new InvalidOperationException(), _ => opened = true, _ => throw new Exception());
        view.ShowBundle(Bundle(false));
        Assert.False(((Button)view.FindName("ArchiveButton")).IsEnabled);
        Assert.DoesNotContain("Report copied", ((TextBlock)view.FindName("SupportStatusText")).Text);
        ((Button)view.FindName("IssueButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(opened);
    });

    [Theory]
    [InlineData(1.5)]
    [InlineData(2)]
    public void SupportActionsWrapAndRemainReachableInScaledLayouts(double scale) => host.Run(() =>
    {
        var view = new FailureSupportView(_ => { }, _ => { }, _ => { });
        view.ShowBundle(Bundle(true));
        var scroll = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var window = new Window { Content = scroll };
        try
        {
            WpfHost.Layout(window, 380, 300);
            var issue = (Button)view.FindName("IssueButton"); var archive = (Button)view.FindName("ArchiveButton");
            var p = archive.TranslatePoint(new Point(), view);
            Assert.True(archive.ActualWidth > 0 && issue.ActualWidth > 0);
            Assert.True(p.X + archive.ActualWidth <= 380);
            var bitmap = new RenderTargetBitmap((int)(380 * scale), (int)(300 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(scroll);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpectedDiscardLeavesBothHostsSupportCardHidden(bool toolkit) => host.Run(() =>
    {
        Window window = toolkit ? new TarkovPerformanceToolkit.MainWindow() : new TarkovPerformanceBenchmark.MainWindow(new(false, false));
        try
        {
            var benchmark = (BenchmarkView)((ContentControl)window.FindName(toolkit ? "BenchmarkRoot" : "ContentRoot")).Content;
            var offer = typeof(BenchmarkView).GetMethod("OfferFailure", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var error in new Exception[] { new OperationCanceledException(), new CaptureDiscardedException("The raid ended before capture completed.") })
                offer.Invoke(benchmark, new object[] { SupportOperation.Capture, error });
            Assert.Equal(Visibility.Collapsed, ((FailureSupportView)benchmark.FindName("FailureSupport")).Visibility);
        }
        finally { window.Close(); }
    });

    private static SupportBundle Bundle(bool archive) => new("sanitized report", new Uri(SupportArchive.IssuesUrl), null,
        archive ? Path.Combine(TestData.Root, "support.zip") : null);
}
