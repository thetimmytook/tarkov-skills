using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TarkovSkills.Core;
using ToolkitWindow = TarkovPerformanceToolkit.MainWindow;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class ToolkitUiTests(WpfHost host)
{
    [Fact]
    public void CloseAndHelpButtonsPersistOnboardingAcrossNewWindows() => host.Run(() =>
    {
        var store = new ToolkitPreferencesStore();
        store.Save(new() { ShowGetStarted = true });
        var first = new ToolkitWindow();
        try
        {
            Assert.Equal(Visibility.Visible, Named<Border>(first, "GettingStartedCard").Visibility);
            Named<Button>(first, "HideGettingStartedButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, Named<Border>(first, "GettingStartedCard").Visibility);
            Assert.False(store.Load().ShowGetStarted);
            Assert.Equal("Show getting started", AutomationProperties.GetName(Named<Button>(first, "GettingStartedButton")));
        }
        finally { first.Close(); }

        var second = new ToolkitWindow();
        try
        {
            Assert.Equal(Visibility.Collapsed, Named<Border>(second, "GettingStartedCard").Visibility);
            Named<Button>(second, "GettingStartedButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.True(store.Load().ShowGetStarted);
            Assert.Equal(Visibility.Visible, Named<Border>(second, "GettingStartedCard").Visibility);
            Assert.Equal("Hide getting started", AutomationProperties.GetName(Named<Button>(second, "GettingStartedButton")));
        }
        finally { second.Close(); }

        var third = new ToolkitWindow();
        try { Assert.Equal(Visibility.Visible, Named<Border>(third, "GettingStartedCard").Visibility); }
        finally { third.Close(); }
    });

    [Fact]
    public void HelpIsOnlyVisibleInOverviewAndReportActionsStartDisabled() => host.Run(() =>
    {
        var window = new ToolkitWindow();
        try
        {
            var tabs = Named<TabControl>(window, "MainTabs");
            var help = Named<Button>(window, "GettingStartedButton");
            Assert.False(Named<Button>(window, "CopyButton").IsEnabled);
            Assert.False(Named<Button>(window, "SaveButton").IsEnabled);
            foreach (var index in new[] { 0, 1, 2, 0 })
            {
                tabs.SelectedIndex = index;
                WpfHost.Layout(window, window.MinWidth, window.MinHeight);
                Assert.Equal(index == 0 ? Visibility.Visible : Visibility.Collapsed, help.Visibility);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SettingReportEnablesActionsWithoutCopyingOrSavingIt() => host.Run(() =>
    {
        var window = new ToolkitWindow();
        try
        {
            typeof(ToolkitWindow).GetMethod("SetReport", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [new { schema_version = 1, test = true }, "Test report ready"]);
            Assert.True(Named<Button>(window, "CopyButton").IsEnabled);
            Assert.True(Named<Button>(window, "SaveButton").IsEnabled);
            Assert.Contains("schema_version", Named<TextBox>(window, "ReportText").Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MinimumWindowRetainsScrollableOverviewAndReachableClose() => host.Run(() =>
    {
        new ToolkitPreferencesStore().Save(new() { ShowGetStarted = true });
        var window = new ToolkitWindow();
        try
        {
            WpfHost.Layout(window, window.MinWidth, window.MinHeight);
            var scroll = Named<ScrollViewer>(window, "OverviewScroll");
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.True(scroll.ScrollableHeight > 0);
            var close = Named<Button>(window, "HideGettingStartedButton");
            Assert.True(close.ActualWidth > 0 && close.ActualHeight > 0);
            Assert.Equal(HorizontalAlignment.Stretch, close.HorizontalAlignment);
            Assert.Equal(1, Grid.GetColumn(close));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MalformedPreferencesDoNotBlockUiAndAreNotOverwrittenOnStartup() => host.Run(() =>
    {
        var path = Path.Combine(AppPaths.DataDirectory, "toolkit-ui.json");
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(path, "invalid test json");
        var window = new ToolkitWindow();
        try
        {
            Assert.Equal(Visibility.Visible, Named<Border>(window, "GettingStartedCard").Visibility);
            Assert.Equal("invalid test json", File.ReadAllText(path));
            Assert.True(Named<Button>(window, "InspectButton").IsEnabled);
        }
        finally { window.Close(); new ToolkitPreferencesStore().Save(new()); }
    });

    [Fact]
    public void PreferenceWriteFailureWarnsWithoutCrashingOrClaimingPersistence() => host.Run(() =>
    {
        var store = new ToolkitPreferencesStore();
        store.Save(new());
        var window = new ToolkitWindow();
        try
        {
            using (File.Open(Path.Combine(AppPaths.DataDirectory, "toolkit-ui.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Named<Button>(window, "HideGettingStartedButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, Named<Border>(window, "GettingStartedCard").Visibility);
                Assert.True(store.Load().ShowGetStarted);
                Assert.Contains("could not be saved", Named<TextBlock>(window, "StatusText").Text);
                Assert.True(Named<Button>(window, "InspectButton").IsEnabled);
            }
        }
        finally { window.Close(); store.Save(new()); }
    });

    private static T Named<T>(Window window, string name) where T : FrameworkElement =>
        Assert.IsType<T>(window.FindName(name));
}
