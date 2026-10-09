using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;
using TarkovSkills.Core.Academy;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class ResourceTelemetryUiTests(WpfHost host)
{
    [Theory]
    [InlineData(false, "nvapi_gpu_graphics_utilization")]
    [InlineData(true, "nvapi_gpu_graphics_utilization")]
    [InlineData(false, "adlx_gpu_usage")]
    [InlineData(true, "adlx_gpu_usage")]
    public void BothHostsIdentifyVendorLoadWithoutChangingWholeAdapterMemoryMeaning(bool toolkit, string source) => host.Run(() =>
    {
        Window window = toolkit ? new TarkovPerformanceToolkit.MainWindow() : new TarkovPerformanceBenchmark.MainWindow(new(false, false));
        try
        {
            if (toolkit) Assert.IsType<TabControl>(window.FindName("MainTabs")).SelectedIndex = 1;
            var root = WpfHost.Layout(window, 760, 560);
            var view = Descendants(root).OfType<BenchmarkView>().Single();
            var resources = Assert.IsType<ResourceTelemetryView>(view.FindName("ResourceResults"));
            var telemetry = Fixture();
            resources.Show(telemetry with { Gpu = telemetry.Gpu with
                { GraphicsUtilization = telemetry.Gpu.GraphicsUtilization with { Source = source } } });
            Assert.IsType<Expander>(resources.FindName("ResourceDetails")).IsExpanded = true;
            root.UpdateLayout();
            Assert.Contains(Descendants(resources).OfType<TextBlock>(), text => text.Text == "GPU graphics load (vendor driver)");
            Assert.Contains(Descendants(resources).OfType<TextBlock>(), text => text.Text == "Shared GPU memory used");
            Assert.Contains("whole adapter", Assert.IsType<TextBlock>(resources.FindName("GpuHeading")).Text);
            Assert.Contains("adds no physical VRAM", Assert.IsType<TextBlock>(resources.FindName("MemoryExplanation")).Text);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false, 700, 520)]
    [InlineData(true, 760, 560)]
    public void BothHostsExposeWholeAdapterSummaryWithoutOverlappingColumns(bool toolkit, double width, double height) => host.Run(() =>
    {
        Window window = toolkit ? new TarkovPerformanceToolkit.MainWindow() : new TarkovPerformanceBenchmark.MainWindow(new(false, false));
        try
        {
            if (toolkit) Assert.IsType<TabControl>(window.FindName("MainTabs")).SelectedIndex = 1;
            var root = WpfHost.Layout(window, width, height);
            var view = Descendants(root).OfType<BenchmarkView>().Single();
            var resources = Assert.IsType<ResourceTelemetryView>(view.FindName("ResourceResults"));
            resources.Show(Fixture());
            Assert.IsType<Expander>(resources.FindName("ResourceDetails")).IsExpanded = true;
            root.UpdateLayout();
            Assert.Contains("whole adapter", Assert.IsType<TextBlock>(resources.FindName("GpuHeading")).Text);
            Assert.Contains("Shared GPU memory uses system RAM", Assert.IsType<TextBlock>(resources.FindName("MemoryExplanation")).Text);
            var hint = Assert.IsType<TextBlock>(resources.FindName("TextureHint"));
            Assert.Equal(Visibility.Visible, hint.Visibility);
            Assert.Contains("does not establish the cause", hint.Text);
            Assert.Contains(Descendants(resources).OfType<TextBlock>(), text => text.Text == "Physical RAM available");
            Assert.Contains(Descendants(resources).OfType<TextBlock>(), text => text.Text == "System commit limit");
            foreach (var grid in Descendants(resources).OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 3))
            {
                var texts = grid.Children.OfType<TextBlock>().OrderBy(Grid.GetColumn).ToArray();
                Assert.Equal(3, texts.Length);
                Assert.True(texts[0].TranslatePoint(new Point(texts[0].ActualWidth, 0), grid).X <=
                    texts[1].TranslatePoint(new Point(0, 0), grid).X + .5);
                Assert.True(texts[1].TranslatePoint(new Point(texts[1].ActualWidth, 0), grid).X <=
                    texts[2].TranslatePoint(new Point(0, 0), grid).X + .5);
            }
            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var output = Environment.GetEnvironmentVariable("TARKOV_TELEMETRY_UI_PREVIEW_DIRECTORY");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, toolkit ? "toolkit-telemetry.png" : "benchmark-telemetry.png"));
                encoder.Save(file);
                // Render a detached copy so the host ScrollViewer cannot clip the preview.
                var detailView = new ResourceTelemetryView();
                detailView.Show(Fixture());
                Assert.IsType<Expander>(detailView.FindName("ResourceDetails")).IsExpanded = true;
                Assert.IsType<Expander>(detailView.FindName("ProcessorDetails")).IsExpanded = true;
                var detailRoot = new Border { Child = detailView, Padding = new Thickness(20),
                    Background = (Brush)Application.Current.FindResource("BackgroundBrush") };
                detailRoot.Measure(new Size(660, double.PositiveInfinity));
                detailRoot.Arrange(new Rect(0, 0, 660, detailRoot.DesiredSize.Height));
                detailRoot.UpdateLayout();
                var detail = new RenderTargetBitmap(660, (int)Math.Ceiling(detailRoot.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                detail.Render(detailRoot);
                var detailEncoder = new PngBitmapEncoder(); detailEncoder.Frames.Add(BitmapFrame.Create(detail));
                using var detailFile = File.Create(Path.Combine(output, "telemetry-detail.png"));
                detailEncoder.Save(detailFile);
            }
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnloadingEitherHostCancelsActiveCaptureAndCannotEmitACompletedRun(bool toolkit) => host.Run(() =>
    {
        var results = new List<CommandResult>();
        var view = new BenchmarkView(toolkit ? BenchmarkFeatureOptions.ForToolkit("test") :
            BenchmarkFeatureOptions.ForStandalone("test", true, true, results.Add));
        using var cancellation = new CancellationTokenSource();
        typeof(BenchmarkView).GetField("_captureCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, cancellation);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.DoesNotContain(results, result => result.Status == "completed" || result.SavedLocally || result.ResourceTelemetry is not null);
    });

    [Fact]
    public void SharedSubmissionReviewShowsFrozenSummaryAndDisclosureWithoutSending() => host.Run(() =>
    {
        var directory = Path.Combine(TestData.Root, "telemetry-review");
        var telemetry = Fixture();
        var run = new BenchmarkRun(Guid.NewGuid().ToString(), "2026-10-06", 120, "test",
            new { cpu = new { name = "Synthetic CPU" }, gpu = new[] { new { name = "Synthetic GPU" } }, ram = new { total_gb = 32 } },
            new { }, new { map = "Woods", execution = "local", weather = "unknown", time_of_day = "day" },
            new(6000, 120, 50, 40, 30, 20, 25, 35), []) { ResourceTelemetry = telemetry };
        var outbox = new SubmissionOutbox(directory, new("https://academy.example/api/bench/v1"), "https://fixture.clerk.accounts.dev", "fixture-client");
        var frozen = outbox.Prepare(run, "Synthetic GPU");
        foreach (var product in new[] { DesktopAuthProduct.Benchmark, DesktopAuthProduct.Toolkit })
        {
            var window = new SubmissionWindow([run with { ResourceTelemetry = ResourceTelemetry.NotCollected }], product);
            try
            {
                typeof(SubmissionWindow).GetMethod("DisplayPrepared", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [frozen]);
                var root = WpfHost.Layout(window, 620, 440);
                Assert.Contains("Resource summary: partial", Assert.IsType<TextBlock>(window.FindName("SummaryText")).Text);
                var review = Assert.IsType<ResourceTelemetryView>(window.FindName("ResourceReview"));
                Assert.Equal(Visibility.Visible, review.Visibility);
                Assert.Contains("partial", Assert.IsType<Expander>(review.FindName("ResourceDetails")).Header.ToString());
                Assert.Contains("Synthetic GPU", Assert.IsType<TextBlock>(review.FindName("GpuHeading")).Text);
                Assert.Equal("Send for review", Assert.IsType<Button>(window.FindName("SendButton")).Content);
                Assert.False(Assert.IsType<Button>(window.FindName("SendButton")).IsEnabled);
                var text = Assert.IsType<TextBlock>(window.FindName("SharingText")).Text;
                Assert.Contains("whole system", text); Assert.Contains("including other applications", text);
                Assert.Contains("Signing in does not upload", text); Assert.Contains("moderator approval", text);
                Assert.Contains("removes its resource summary", Assert.IsType<TextBlock>(window.FindName("DeletionText")).Text);
                var scroll = Assert.IsType<ScrollViewer>(window.FindName("ReviewScroll"));
                Assert.True(scroll.ScrollableHeight > 0);
                Assert.NotNull(window.GetBindingExpression(FrameworkElement.MaxHeightProperty));
                // Rendering a prepared review never creates a confirmed/publication status.
                Assert.Empty(Directory.GetFiles(directory, "*.status.json", SearchOption.AllDirectories));
                var preview = Environment.GetEnvironmentVariable("TARKOV_TELEMETRY_UI_PREVIEW_DIRECTORY");
                if (!string.IsNullOrEmpty(preview))
                {
                    Directory.CreateDirectory(preview);
                    WpfHost.Layout(window, 620, 880);
                    var bitmap = new RenderTargetBitmap(620, 880, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(preview, "submission-telemetry.png")); encoder.Save(file);
                }
            }
            finally
            {
                // No window is shown or account restoration started. Stop the constructed
                // controller and close without the modal dialog's asynchronous close path.
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var controller = typeof(SubmissionWindow).GetField("account", flags)!.GetValue(window)!;
                ((Task)controller.GetType().GetMethod("StopAsync")!.Invoke(controller, null)!).GetAwaiter().GetResult();
                ((IDisposable?)typeof(SubmissionWindow).GetField("api", flags)!.GetValue(window))?.Dispose();
                var close = (System.ComponentModel.CancelEventHandler)Delegate.CreateDelegate(typeof(System.ComponentModel.CancelEventHandler), window,
                    typeof(SubmissionWindow).GetMethod("OnClosing", flags | BindingFlags.DeclaredOnly)!);
                window.Closing -= close; window.Close();
                ((CancellationTokenSource)typeof(SubmissionWindow).GetField("lifetime", flags)!.GetValue(window)!).Dispose();
            }
        }
    });

    [Fact]
    public void LegacyAndUnavailableRunsHaveExplicitUiStates() => host.Run(() =>
    {
        var view = new ResourceTelemetryView();
        view.Show(ResourceTelemetry.NotCollected);
        var expander = Assert.IsType<Expander>(view.FindName("ResourceDetails"));
        Assert.False(expander.IsEnabled);
        Assert.Contains("not collected", expander.Header.ToString());
        view.Show(Fixture() with { Status = "unavailable" });
        Assert.True(expander.IsEnabled);
        Assert.Contains("unavailable", expander.Header.ToString());
    });

    private static ResourceTelemetry Fixture()
    {
        ResourceMetric Metric(double value, string unit = "bytes", string scope = "whole_system", string source = "get_performance_info") =>
            new(value, unit == "count" ? value : value * .8, value, value, 119, 119, .991667, unit, source, scope, "partial", ["partial_coverage"]);
        ResourceCapacity Capacity(double value, string scope = "whole_system", string source = "get_performance_info_physical_total") => new(value, "bytes", source, scope, "available", []);
        const double gib = 1073741824;
        return new(1, "partial", new(120, 120, 1, 120, "presentmon_qpc_valid_frame_intervals", "valid_interval_duration_gauges_capped_at_one_second"),
            new(Metric(35, "percent", source: "pdh_processor_information_processor_time"), [new(0, 0, Metric(95, "percent", "logical_processor", "pdh_processor_information_processor_time"))]),
            new("Synthetic GPU", "whole_adapter", "discrete", "single_hardware_adapter", "selected", Capacity(8 * gib, "whole_adapter", "dxgi_dedicated_video_memory"),
                Metric(90, "percent", "whole_adapter", "pdh_gpu_engine_3d_busiest_engine"), Metric(7.8 * gib, "bytes", "whole_adapter", "pdh_gpu_adapter_memory_dedicated"), Metric(1 * gib, "bytes", "whole_adapter", "pdh_gpu_adapter_memory_shared")),
            new(Capacity(32 * gib, source: "get_physically_installed_system_memory"), Capacity(31.75 * gib), Metric(28 * gib), Metric(3.75 * gib)),
            new(true, Metric(1, "count", source: "enum_page_files"), Metric(16 * gib, source: "enum_page_files"), Metric(4 * gib, source: "enum_page_files"), []),
            new(Metric(35 * gib), Metric(47 * gib), Metric(12 * gib)), ["partial_coverage"]);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
