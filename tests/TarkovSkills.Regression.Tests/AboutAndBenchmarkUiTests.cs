using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovBenchmark.Feature;
using Standalone = TarkovPerformanceBenchmark.MainWindow;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class AboutAndBenchmarkUiTests(WpfHost host)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothAboutWindowsContainSkillsAndScrollOnShortLayout(bool toolkit) => host.Run(() =>
    {
        Window window = toolkit ? new TarkovPerformanceToolkit.AboutWindow() : new TarkovPerformanceBenchmark.AboutWindow();
        try
        {
            var content = WpfHost.Layout(window, 500, 420);
            var descendants = Descendants(content).ToArray();
            Assert.Contains(descendants.OfType<TextBlock>(), text => text.Text == "AI-assisted analysis");
            Assert.Contains(descendants.OfType<Button>(), button => Equals(button.Content, "Set up AI skills"));
            Assert.Contains(descendants.OfType<Button>(), button => Equals(button.Content, "Close"));
            var scroll = Assert.Single(descendants.OfType<ScrollViewer>(), s => s.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
            Assert.True(scroll.ScrollableHeight > 0);
            Assert.NotNull(window.GetBindingExpression(FrameworkElement.MaxHeightProperty));
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(700, 520, 1)]
    [InlineData(960, 820, 1)]
    [InlineData(700, 520, 1.5)]
    public void StandaloneFooterKeepsSpacingAndDoesNotOverlap(double width, double height, double scale) => host.Run(() =>
    {
        var window = new Standalone(new(false, false));
        try
        {
            var root = WpfHost.Layout(window, width, height);
            var footer = Assert.IsType<Border>(window.FindName("Footer"));
            var author = Assert.IsType<TextBlock>(window.FindName("FooterAuthor"));
            var actions = Assert.IsType<StackPanel>(window.FindName("FooterActions"));
            var about = Assert.IsType<Button>(window.FindName("AboutButton"));
            Assert.Equal(new Thickness(24, 12, 24, 16), footer.Padding);
            Assert.Equal(VerticalAlignment.Center, about.VerticalAlignment);
            Assert.True(author.TranslatePoint(new Point(author.ActualWidth, 0), root).X <= actions.TranslatePoint(new Point(0, 0), root).X + 0.5);
            Assert.True(root.ActualHeight - about.TranslatePoint(new Point(0, about.ActualHeight), root).Y >= 16);
            Assert.True(root.ActualWidth - about.TranslatePoint(new Point(about.ActualWidth, 0), root).X >= 24);
            var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root);
            Assert.Equal((int)(width * scale), bitmap.PixelWidth);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SharedBenchmarkShowsWebCopyOnlyInToolkitAndStartsWithCancelDisabled() => host.Run(() =>
    {
        var toolkit = new BenchmarkView(BenchmarkFeatureOptions.ForToolkit("test"));
        var standalone = new BenchmarkView(BenchmarkFeatureOptions.ForStandalone("test", false, false, null));
        Assert.Equal(Visibility.Visible, Assert.IsType<Button>(toolkit.FindName("CopyResultsButton")).Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(standalone.FindName("CopyResultsButton")).Visibility);
        Assert.False(Assert.IsType<Button>(toolkit.FindName("CancelButton")).IsEnabled);
        Assert.False(Assert.IsType<Button>(standalone.FindName("CancelButton")).IsEnabled);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectionStateDisablesStartAndEnablesCancelInBothShells(bool isToolkit) => host.Run(() =>
    {
        var options = isToolkit ? BenchmarkFeatureOptions.ForToolkit("test")
            : BenchmarkFeatureOptions.ForStandalone("test", false, false, null);
        var view = new BenchmarkView(options);
        var setter = typeof(BenchmarkView).GetMethod("SetCollecting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        setter.Invoke(view, [true]);
        Assert.False(Assert.IsType<Button>(view.FindName("StartButton")).IsEnabled);
        Assert.True(Assert.IsType<Button>(view.FindName("CancelButton")).IsEnabled);
        setter.Invoke(view, [false]);
        Assert.False(Assert.IsType<Button>(view.FindName("CancelButton")).IsEnabled);
    });

    [Fact]
    public void LegacyCollectInvocationRetainsExplicitGuiStartContract()
    {
        var invocation = TarkovPerformanceBenchmark.AppInvocation.Parse(["collect", "--source", "skill"]);
        Assert.True(invocation.CollectRequested);
        Assert.True(invocation.SourceSkill);
        Assert.False(TarkovPerformanceBenchmark.AppInvocation.Parse([]).CollectRequested);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
