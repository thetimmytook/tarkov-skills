using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TarkovSkills.Core;

namespace TarkovBenchmark.Feature;

public partial class GoalSettingsView : UserControl
{
    private readonly GoalStore _store;
    public GoalSettingsView() : this(new GoalStore()) { }

    internal GoalSettingsView(GoalStore store)
    {
        InitializeComponent();
        _store = store;
        try
        {
            var goal = _store.Load();
            GoalText.Text = goal.Goal;
            TargetText.Text = goal.TargetFpsMin.ToString();
            QualityText.Text = goal.QualityPreference;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveGoalButton.IsEnabled = false;
            ShowStatus("Your saved goal could not be opened. Check access to the local data folder.", true);
        }
    }

    private void SaveGoal_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TargetText.Text, out var target) || target is < 20 or > 360)
        { ShowStatus("Target FPS must be a whole number between 20 and 360.", true); return; }
        if (string.IsNullOrWhiteSpace(GoalText.Text))
        { ShowStatus("Goal is required.", true); return; }
        if (string.IsNullOrWhiteSpace(QualityText.Text))
        { ShowStatus("Quality preference is required.", true); return; }
        try
        {
            var goal = _store.Save(GoalText.Text, target, QualityText.Text, _store.Load().Notes);
            ShowStatus($"Goal saved locally: {goal.TargetFpsMin} FPS.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { ShowStatus("The goal could not be saved. Check access to the local data folder and try again.", true); }
    }

    private void ShowStatus(string text, bool error)
    {
        GoalStatusText.Text = text;
        GoalStatusText.Foreground = (Brush)FindResource(error ? "ErrorBrush" : "TextBrush");
    }
}
