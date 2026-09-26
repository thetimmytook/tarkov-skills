using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TarkovBenchmark.Feature;

// The shell supplies its viewport; one rule serves both hosts, including tab changes.
public static class ContentHeightLimit
{
    public static void Attach(Window window, ScrollViewer viewport, BenchmarkView content)
    {
        var preferredHeight = window.Height;
        var appliedHeight = double.NaN;
        var applying = false;
        window.SizeChanged += (_, args) =>
        {
            if (!applying && args.HeightChanged && window.WindowState == WindowState.Normal &&
                (double.IsNaN(appliedHeight) || Math.Abs(args.NewSize.Height - appliedHeight) > 1))
                preferredHeight = args.NewSize.Height;
        };
        void Update()
        {
            if (!content.IsLoaded || !content.IsVisible || content.IsComparisonLoading || viewport.ActualHeight <= 0) return;
            var chrome = Math.Max(0, window.ActualHeight - viewport.ActualHeight);
            var maximum = Math.Max(window.MinHeight,
                Math.Min(SystemParameters.WorkArea.Height, content.ActualHeight + chrome));
            applying = true;
            try
            {
                appliedHeight = Math.Min(preferredHeight, maximum);
                if (Math.Abs(window.MaxHeight - maximum) > 0.5) window.MaxHeight = maximum;
                if (window.WindowState == WindowState.Normal && Math.Abs(window.Height - appliedHeight) > 0.5)
                    window.Height = appliedHeight;
            }
            finally { applying = false; }
        }
        void QueueUpdate(object? sender, RoutedEventArgs args) =>
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Update));

        content.Loaded += QueueUpdate;
        content.SizeChanged += QueueUpdate;
        viewport.SizeChanged += QueueUpdate;
        content.ComparisonLayoutChanged += (_, _) =>
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Update));
        content.Unloaded += (_, _) => window.MaxHeight = Math.Max(window.MinHeight, SystemParameters.WorkArea.Height);
    }
}
