using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TarkovSkills.Core;
using TarkovSkills.Core.Academy;

namespace TarkovBenchmark.Feature;

public partial class ComparisonView : UserControl
{
    private CancellationTokenSource? pending;
    public bool IsLoading => pending is not null;
    public event EventHandler? LayoutStateChanged;
    private bool updating;
    private sealed record MapChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }
    private sealed record RunChoice(BenchmarkRun? Run)
    {
        public override string ToString() => Run is null ? "Public runs by map" :
            $"{Run.CollectedDate} · {Run.Performance.AverageFps:0.0} FPS · {Run.RunId[..Math.Min(8, Run.RunId.Length)]}";
    }

    public ComparisonView()
    {
        InitializeComponent();
        updating = true;
        MapPicker.ItemsSource = BenchmarkComparison.Maps.Select(map => new MapChoice(map.Key, map.Value)).ToArray();
        MapPicker.SelectedValue = "streets";
        updating = false;
        Loaded += (_, _) => _ = RefreshAsync();
        Unloaded += (_, _) => pending?.Cancel();
    }

    public void SetRuns(IReadOnlyList<BenchmarkRun> runs)
    {
        updating = true;
        var selected = (RunPicker.SelectedItem as RunChoice)?.Run?.RunId;
        var choices = new[] { new RunChoice(null) }.Concat(runs.Reverse().Select(run => new RunChoice(run))).ToArray();
        RunPicker.ItemsSource = choices;
        RunPicker.SelectedItem = choices.FirstOrDefault(item => selected is not null && item.Run?.RunId == selected)
            ?? (runs.Count > 0 ? choices[1] : choices[0]);
        IntroText.Text = runs.Count == 0 ? "Run benchmark to see your position" :
            "Compare a local run with matching public runs, or explore a map. Your run is highlighted in green.";
        updating = false;
        UpdateSelection();
    }

    private void RunChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updating) UpdateSelection();
    }

    private void UpdateSelection()
    {
        updating = true;
        var run = (RunPicker.SelectedItem as RunChoice)?.Run;
        MapPicker.IsEnabled = run is null;
        GpuPicker.Visibility = run is null ? Visibility.Collapsed : Visibility.Visible;
        try
        {
            GpuPicker.ItemsSource = run is null ? Array.Empty<string>() : SubmissionPayload.GpuNames(run);
            GpuPicker.SelectedIndex = GpuPicker.Items.Count == 1 ? 0 : -1;
            if (run is not null && GpuPicker.SelectedItem is string gpu)
            {
                var query = System.Text.Json.Nodes.JsonNode.Parse(BenchmarkComparison.Query(run, gpu))!;
                MapPicker.SelectedValue = query["map"]!.GetValue<string>();
            }
        }
        catch { MapPicker.SelectedIndex = -1; }
        updating = false;
        if (IsLoaded) _ = RefreshAsync();
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updating && IsLoaded) _ = RefreshAsync();
    }
    private void RefreshClicked(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        pending?.Cancel();
        using var request = new CancellationTokenSource();
        pending = request;
        LayoutStateChanged?.Invoke(this, EventArgs.Empty);
        BarsPanel.Children.Clear();
        ScaleText.Text = "";
        DescriptionText.Text = "Loading public benchmarks…";
        try
        {
            using var client = new PublicBenchmarkClient(AcademyConfiguration.Read(Path.Combine(AppContext.BaseDirectory, "academy-api.json")));
            ComparisonResult result;
            if ((RunPicker.SelectedItem as RunChoice)?.Run is { } run)
            {
                if (GpuPicker.SelectedItem is not string gpu)
                { DescriptionText.Text = "Choose the GPU used for this run."; return; }
                updating = true;
                try
                {
                    MapPicker.SelectedValue = System.Text.Json.Nodes.JsonNode.Parse(BenchmarkComparison.Query(run, gpu))!["map"]!.GetValue<string>();
                }
                finally { updating = false; }
                result = await client.CompareAsync(run, gpu, request.Token);
            }
            else
            {
                if (MapPicker.SelectedValue is not string map) return;
                result = await client.BrowseAsync(map, request.Token);
            }
            if (request.IsCancellationRequested) return;
            DescriptionText.Text = result.Description;
            Render(result.Runs);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (InvalidDataException)
        { if (!request.IsCancellationRequested) DescriptionText.Text = "This run or response cannot be compared. Choose Public runs by map to browse."; }
        catch
        { if (!request.IsCancellationRequested) DescriptionText.Text = "Public benchmarks are unavailable. Check your connection and choose Refresh comparison."; }
        finally
        {
            if (ReferenceEquals(pending, request))
            {
                pending = null;
                LayoutStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void Render(IReadOnlyList<ComparisonBar> rows)
    {
        if (rows.Count == 0) { ScaleText.Text = "No public runs on this map yet."; return; }
        var maximum = Math.Ceiling(rows.Max(row => row.AverageFps) / 10) * 10;
        ScaleText.Text = $"Shared scale: 0–{maximum:0} FPS · {rows.Count} runs shown";
        foreach (var row in rows.OrderByDescending(row => row.IsLocal).ThenByDescending(row => row.AverageFps))
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = row.Label + (row.IsSynthetic ? " · Demo" : ""),
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = row.Conditions, Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            AddBar(content, row.AverageFps, maximum, "Average FPS", row.IsLocal ? "AccentBrush" : "ActionBrush");
            AddBar(content, row.OnePercentLowFps, maximum, "1% Low", row.IsLocal ? "ReadyBrush" : "InfoBrush");
            BarsPanel.Children.Add(new Border { Child = content, Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource(row.IsLocal ? "SuccessBorderBrush" : "BorderBrush"),
                Background = (Brush)FindResource(row.IsLocal ? "SuccessBackgroundBrush" : "BackgroundBrush") });
        }
    }

    private void AddBar(Panel parent, double value, double maximum, string label, string brush)
    {
        var line = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        var bar = new Grid { Height = 12, VerticalAlignment = VerticalAlignment.Center };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(value / maximum, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - value / maximum, GridUnitType.Star) });
        bar.Children.Add(new Border { Background = (Brush)FindResource(brush), CornerRadius = new CornerRadius(3) });
        line.Children.Add(bar);
        var number = new TextBlock { Text = $"{label}  {value:0.0}", Margin = new Thickness(12, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(number, 1);
        line.Children.Add(number);
        parent.Children.Add(line);
    }
}
