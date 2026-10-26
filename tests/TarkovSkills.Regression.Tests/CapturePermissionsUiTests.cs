using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovBenchmark.Feature;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class CapturePermissionsUiTests(WpfHost host)
{
    [Fact]
    public void CaptureScreenDoesNotExposeAccountPermissionManagement() => host.Run(() =>
    {
        var view = new BenchmarkView();
        Assert.Null(view.FindName("CaptureAccessButton"));
    });

    [Fact]
    public void SettingsDoesNotRequireAnIntermediateManageButton() => host.Run(() =>
    {
        using var session = new CaptureAccessSession();
        var settings = new SettingsView(session);
        Assert.Null(settings.FindName("ManageAccessButton"));
        var access = Assert.IsType<CaptureAccessView>(((ContentControl)settings.FindName("AccessSection")).Content);
        Assert.Null(access.FindName("ManageAccessButton"));
        Assert.IsType<Button>(access.FindName("RefreshAccessButton"));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckingPermissionAndDecliningConsentCannotLaunchSetup(bool hasAccess) => host.Run(() =>
    {
        var launches = 0;
        var window = AccessWindow(() => new(hasAccess, false, false),
            (_, _) => { launches++; return Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Added)); }, _ => false);
        try
        {
            WpfHost.Layout(window, 560, 480);
            Assert.Equal(0, launches);
            if (!hasAccess) FindButton(window, "AddAccessButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, launches);
            Assert.False(FindButton(window, "RemoveAccessButton").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SuccessfulSetupAllowsCaptureImmediatelyAndDoesNotOfferRepeatedElevation() => host.Run(() =>
    {
        var state = new EtwPermissionState(false, false, false);
        var temporaryActive = false;
        var window = AccessWindow(() => state, (action, _) =>
        {
            Assert.Equal(EtwPermissionAction.Add, action);
            state = new(false, true, false);
            temporaryActive = true;
            return Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Added));
        }, _ => true, () => temporaryActive);
        try
        {
            FindButton(window, "AddAccessButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains("Ready to capture now", Text(window));
            Assert.DoesNotContain("Sign out of Windows", Text(window));
            Assert.False(FindButton(window, "AddAccessButton").IsEnabled);
            Assert.True(FindButton(window, "RemoveAccessButton").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void UacCancellationKeepsInlineAccessUsableAndLeavesTheAccountUnconfigured() => host.Run(() =>
    {
        var window = AccessWindow(() => new(false, false, false),
            (_, _) => Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Cancelled)), _ => true);
        try
        {
            FindButton(window, "AddAccessButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains("cancelled", Text(window));
            Assert.True(FindButton(window, "AddAccessButton").IsEnabled);
            Assert.True(FindButton(window, "RefreshAccessButton").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ExplicitRemovalExplainsPersistentCurrentTokenAndDoesNotRemoveInheritedAccess() => host.Run(() =>
    {
        var state = new EtwPermissionState(true, true, false);
        var window = AccessWindow(() => state, (action, _) =>
        {
            Assert.Equal(EtwPermissionAction.Remove, action);
            state = new(true, false, false);
            return Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Removed));
        }, _ => true);
        try
        {
            FindButton(window, "RemoveAccessButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains("Sign out of Windows", Text(window));
            Assert.Contains("another group is unchanged", Text(window));
            Assert.False(FindButton(window, "RemoveAccessButton").IsEnabled);
            Assert.False(FindButton(window, "AddAccessButton").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ClosingTheHostCancelsAnUnfinishedSetupRequest() => host.Run(() =>
    {
        var completed = new TaskCompletionSource<EtwPermissionResult>();
        CancellationToken token = default;
        var window = AccessWindow(() => new(false, false, false),
            (_, cancellation) => { token = cancellation; return completed.Task; }, _ => true);
        FindButton(window, "AddAccessButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(FindButton(window, "AddAccessButton").IsEnabled);
        window.Close();
        Assert.True(token.IsCancellationRequested);
        completed.SetResult(new(EtwPermissionOutcome.Cancelled));
    });

    [Theory]
    [InlineData(430, 260, 1)]
    [InlineData(560, 420, 1.5)]
    [InlineData(560, 420, 2)]
    public void InlinePermissionSectionWrapsAndScrollsOnSmallAndScaledLayouts(double width, double height, double scale) => host.Run(() =>
    {
        var window = AccessWindow(() => new(false, false, false),
            (_, _) => throw new InvalidOperationException("Layout cannot start setup"), _ => false);
        try
        {
            ((Expander)Access(window).FindName("AccessHelp")).IsExpanded = true;
            var root = WpfHost.Layout(window, width, height);
            var scroll = Assert.IsType<ScrollViewer>(root);
            Assert.True(scroll.ScrollableHeight > 0);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root);
            Assert.Equal((int)(width * scale), bitmap.PixelWidth);
            Assert.Equal(Visibility.Visible, Access(window).FindName("RemoveAccessSection") is Border section ? section.Visibility : Visibility.Collapsed);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false, "--setup-permissions")]
    [InlineData(true, "--setup-permissions")]
    [InlineData(false, "--capture-helper")]
    [InlineData(true, "--capture-helper")]
    public async Task InvalidHelperInvocationInEitherHostExitsBeforeNormalStartup(bool toolkit, string helperArgument)
    {
        // Project-reference copies in the test output do not constitute a complete
        // self-contained application. Launch each host's real build layout.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var product = toolkit ? "toolkit" : "benchmark";
        var name = toolkit ? "TarkovPerformanceToolkit" : "TarkovPerformanceBenchmark";
        var executable = Path.Combine(root.FullName, "apps", "tarkov-performance-" + product, "src", name,
            "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64", name + ".exe");
        var isolated = Path.Combine(TestData.Root, "invalid-helper-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(helperArgument); start.ArgumentList.Add("invalid-operation");
        start.Environment["TARKOV_BENCHMARK_TEST_DATA_DIRECTORY"] = isolated;
        using var process = Process.Start(start)!;
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal((int)EtwPermissionOutcome.InvalidRequest, process.ExitCode);
            Assert.False(Directory.Exists(isolated));
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2)]
    public void SettingsManagementIsReachableOnScaledLayoutsAndDiagnosticProbeIsAbsentFromNormalBuilds(double scale) => host.Run(() =>
    {
        using var session = new CaptureAccessSession();
        var settings = new SettingsView(session);
        var window = new Window { Content = settings };
        try
        {
            var root = WpfHost.Layout(window, 430, 300);
            var access = Assert.IsType<CaptureAccessView>(((ContentControl)settings.FindName("AccessSection")).Content);
            var check = Assert.IsType<Button>(access.FindName("RefreshAccessButton"));
            Assert.True(check.ActualWidth > 0 && check.ActualHeight > 0);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(access.FindName("DiagnosticProbePanel")).Visibility);
            var bitmap = new RenderTargetBitmap((int)(430 * scale), (int)(300 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root); Assert.Equal((int)(430 * scale), bitmap.PixelWidth);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothHostsKeepSharedPermissionManagementInSettingsAndDisableItDuringCapture(bool toolkit) => host.Run(() =>
    {
        Window window = toolkit ? new TarkovPerformanceToolkit.MainWindow() : new TarkovPerformanceBenchmark.MainWindow(new(false, false));
        try
        {
            var settings = Assert.IsType<TabItem>(window.FindName("SettingsTab"));
            Assert.Equal("Settings", settings.Header);
            var root = Assert.IsType<ContentControl>(window.FindName("SettingsRoot"));
            var shared = Assert.IsType<SettingsView>(root.Content);
            Assert.Equal(toolkit ? SettingsSections.Access | SettingsSections.Goal : SettingsSections.Access, shared.Sections);
            Assert.Equal(toolkit, ((ContentControl)shared.FindName("GoalSection")).Content is GoalSettingsView);
            var tabs = Assert.IsType<TabControl>(window.FindName("MainTabs"));
            Assert.DoesNotContain(tabs.Items.Cast<TabItem>(), tab => Equals(tab.Header, "Goal"));
            var benchmarkRoot = Assert.IsType<ContentControl>(window.FindName(toolkit ? "BenchmarkRoot" : "ContentRoot"));
            var benchmark = Assert.IsType<BenchmarkView>(benchmarkRoot.Content);
            var setter = typeof(BenchmarkView).GetMethod("SetCollecting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            setter.Invoke(benchmark, [true]); Assert.False(settings.IsEnabled);
            setter.Invoke(benchmark, [false]); Assert.True(settings.IsEnabled);
            Assert.False(benchmark.CaptureAccess.TemporaryCollectorActive);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(SettingsSections.Access, 1, 0)]
    [InlineData(SettingsSections.Goal, 0, 1)]
    [InlineData(SettingsSections.Access | SettingsSections.Goal, 1, 1)]
    public void SettingsOnlyConstructsSelectedSections(SettingsSections sections, int expectedAccess, int expectedGoal) => host.Run(() =>
    {
        var access = 0; var goal = 0;
        var settings = new SettingsView(sections, () => { access++; return new UserControl(); },
            () => { goal++; return new UserControl(); });
        Assert.Equal(expectedAccess, access);
        Assert.Equal(expectedGoal, goal);
        Assert.Equal(expectedGoal == 1 ? Visibility.Visible : Visibility.Collapsed,
            ((ContentControl)settings.FindName("GoalSection")).Visibility);
    });

    [Theory]
    [InlineData(430)]
    [InlineData(760)]
    public void AccessActionsHaveSpacingWhenInlineOrWrappedAndRemovalIsAlwaysVisible(double width) => host.Run(() =>
    {
        var window = AccessWindow(() => new(false, true, false), (_, _) => throw new InvalidOperationException(), _ => false);
        try
        {
            WpfHost.Layout(window, width, 900);
            var check = FindButton(window, "RefreshAccessButton");
            var reports = FindButton(window, "OpenReportsButton");
            var checkPosition = check.TranslatePoint(new Point(), Access(window));
            var reportsPosition = reports.TranslatePoint(new Point(), Access(window));
            var gap = Math.Abs(checkPosition.Y - reportsPosition.Y) < 1
                ? reportsPosition.X - checkPosition.X - check.ActualWidth
                : reportsPosition.Y - checkPosition.Y - check.ActualHeight;
            Assert.True(gap >= 11.9, $"Action gap was {gap} DIP at width {width}.");
            var removal = (Border)Access(window).FindName("RemoveAccessSection");
            Assert.Equal(Visibility.Visible, removal.Visibility);
            Assert.True(removal.ActualHeight > FindButton(window, "RemoveAccessButton").ActualHeight);
            Assert.Empty(Descendants<Expander>(removal));
            Assert.Null(FindAncestor<Expander>(removal));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SwitchingAwayCancelsSetupButReturningAllowsAnotherAttempt() => host.Run(() =>
    {
        var pending = new TaskCompletionSource<EtwPermissionResult>();
        var tokens = new List<CancellationToken>();
        var access = new CaptureAccessView(() => new(false, false, false), (_, token) =>
        {
            tokens.Add(token);
            return tokens.Count == 1 ? pending.Task : Task.FromResult(new EtwPermissionResult(EtwPermissionOutcome.Cancelled));
        }, _ => true);
        var add = (Button)access.FindName("AddAccessButton");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        access.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.True(tokens[0].IsCancellationRequested);
        pending.SetResult(new(EtwPermissionOutcome.Cancelled));
        access.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.True(add.IsEnabled);
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, tokens.Count);
        Assert.False(tokens[1].IsCancellationRequested);
        Assert.True(add.IsEnabled);
    });

    [Theory]
    [InlineData(430)]
    [InlineData(760)]
    public void AccessHelpStartsCollapsedAndNeverHidesManagementActions(double width) => host.Run(() =>
    {
        var launches = 0;
        var window = AccessWindow(() => new(false, true, false),
            (_, _) => { launches++; throw new InvalidOperationException(); }, _ => false);
        try
        {
            WpfHost.Layout(window, width, 420);
            var access = Access(window);
            var help = Assert.IsType<Expander>(access.FindName("AccessHelp"));
            Assert.False(help.IsExpanded);
            var closedHeight = access.ActualHeight;
            help.IsExpanded = true;
            WpfHost.Layout(window, width, 420);
            Assert.True(access.ActualHeight > closedHeight + 100);
            help.IsExpanded = false;
            WpfHost.Layout(window, width, 420);
            foreach (var name in new[] { "AddAccessButton", "RefreshAccessButton", "OpenReportsButton", "RemoveAccessButton" })
            {
                var button = FindButton(window, name);
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Null(FindAncestor<Expander>(button));
            }
            Assert.Equal(0, launches);
        }
        finally { window.Close(); }
    });

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T match) return match;
        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static Window AccessWindow(Func<EtwPermissionState> check,
        Func<EtwPermissionAction, CancellationToken, Task<EtwPermissionResult>> change,
        Func<EtwPermissionAction, bool> confirm, Func<bool>? temporaryActive = null)
    {
        var access = new CaptureAccessView(check, change, confirm, temporaryActive);
        var window = new Window { Content = new ScrollViewer
        {
            Content = access, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        } };
        window.Closed += (_, _) => access.CancelPendingOperations();
        return window;
    }

    private static CaptureAccessView Access(Window window) => (CaptureAccessView)((ScrollViewer)window.Content).Content;
    private static Button FindButton(Window window, string name) => Assert.IsType<Button>(Access(window).FindName(name));
    private static string Text(Window window) => Assert.IsType<TextBlock>(Access(window).FindName("AccessStatusText")).Text;
}
