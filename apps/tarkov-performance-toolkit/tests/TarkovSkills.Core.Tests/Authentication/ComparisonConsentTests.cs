using System.Windows;
using System.Windows.Controls;
using TarkovBenchmark.Feature;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class ComparisonConsentTests
{
    [Fact]
    public async Task SavedRunIsNotSelectedByDefaultAndLoadedLocalSelectionWaitsForCompare()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application();
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/TarkovBenchmark.Feature;component/Themes/TimmyAcademy.xaml", UriKind.Relative) });
                var view = new ComparisonView();
                view.SetRuns([SubmissionTests.Run()]);
                var picker = (ComboBox)view.FindName("RunPicker");
                Assert.Equal(0, picker.SelectedIndex);
                Assert.Equal("Public runs by map", picker.SelectedItem.ToString());
                picker.SelectedIndex = 1;
                // A reload/tab activation must not send even a previously selected local run.
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.StartsWith("Choose Compare selected run", ((TextBlock)view.FindName("DescriptionText")).Text);
                Assert.Equal("Compare selected run", ((Button)view.FindName("RefreshButton")).Content);
                Assert.False(view.IsLoading);
                app.Shutdown();
                completion.SetResult();
            }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
