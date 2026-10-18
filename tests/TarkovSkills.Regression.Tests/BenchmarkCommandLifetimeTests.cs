using System.Reflection;
using System.Windows;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class BenchmarkCommandLifetimeTests(WpfHost host)
{
    [Fact]
    public void SettingsNavigationDoesNotCompleteASkillCommandBeforeTheWindowCloses() => host.Run(() =>
    {
        var results = new List<CommandResult>();
        var view = new BenchmarkView(BenchmarkFeatureOptions.ForStandalone("1.1.0", true, true, results.Add));
        var window = new Window { Content = view };
        try
        {
            Invoke(view, "OnLoaded", view, new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Empty(results);
            Invoke(view, "OnLoaded", view, new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Empty(results);
            window.Close();
            Assert.Equal("cancelled", Assert.Single(results).Status);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Single(results);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedOutputIsNotReplacedWhenClosingOrWhenItsCheckpointFails(bool checkpointFails) => host.Run(() =>
    {
        var results = new List<CommandResult>();
        var view = new BenchmarkView(BenchmarkFeatureOptions.ForStandalone("1.1.0", true, true, result =>
        {
            results.Add(result);
            if (checkpointFails) throw new IOException("The command checkpoint is locked.");
        }));
        var support = new FailureSupportView(_ => { }, _ => throw new Exception("No browser"), _ => throw new Exception("No Explorer"));
        typeof(BenchmarkView).GetField("FailureSupport", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(view, support);
        var window = new Window { Content = view };
        try
        {
            Invoke(view, "OnLoaded", view, new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var completed = new CommandResult("completed", Guid.NewGuid().ToString(), "Factory", 100, 80, 60, 12, true);
            Assert.Null(Record.Exception(() => Invoke(view, "WriteResult", completed)));
            Invoke(view, "WriteTerminalResult", "failed", "Must not replace a completed result.");
            window.Close();
            Assert.Same(completed, Assert.Single(results));
            Assert.True(results[0].SavedLocally);
            Assert.Equal(checkpointFails ? Visibility.Visible : Visibility.Collapsed, support.Visibility);
            if (checkpointFails)
                Assert.Contains("saved locally", ((System.Windows.Controls.TextBlock)view.FindName("StatusText")).Text);
        }
        finally { window.Close(); }
    });

    private static void Invoke(BenchmarkView view, string method, params object[] args) =>
        typeof(BenchmarkView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(view, args);
}
