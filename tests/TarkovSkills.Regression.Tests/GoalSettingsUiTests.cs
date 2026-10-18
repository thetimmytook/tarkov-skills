using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovBenchmark.Feature;
using TarkovSkills.Core;

namespace TarkovSkills.Regression.Tests;

[Collection("WPF")]
public sealed class GoalSettingsUiTests(WpfHost host)
{
    private static GoalStore Store() => new(Path.Combine(TestData.Root, "goal-" + Guid.NewGuid().ToString("N"), "current-goal.json"));

    [Fact]
    public void MovedGoalEditorLoadsExistingValuesAndSavesLocallyWithoutLosingNotes() => host.Run(() =>
    {
        var store = Store();
        store.Save("stable-fps", 72, "visibility", "Keep these agent notes.");
        var view = new GoalSettingsView(store);
        Assert.Equal("72", Field(view, "TargetText").Text);
        Assert.Equal("visibility", Field(view, "QualityText").Text);
        Field(view, "GoalText").Text = "better-lows";
        Field(view, "TargetText").Text = "90";
        Field(view, "QualityText").Text = "textures";
        ((Button)view.FindName("SaveGoalButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var saved = store.Load();
        Assert.Equal("better-lows", saved.Goal);
        Assert.Equal(90, saved.TargetFpsMin);
        Assert.Equal("textures", saved.QualityPreference);
        Assert.Equal("Keep these agent notes.", saved.Notes);
        Assert.Contains("saved locally", ((TextBlock)view.FindName("GoalStatusText")).Text);
    });

    [Theory]
    [InlineData("invalid", "stable-fps", "balanced")]
    [InlineData("19", "stable-fps", "balanced")]
    [InlineData("361", "stable-fps", "balanced")]
    [InlineData("60", " ", "balanced")]
    [InlineData("60", "stable-fps", " ")]
    public void InvalidGoalDoesNotReplaceSavedValues(string target, string goal, string quality) => host.Run(() =>
    {
        var store = Store();
        var before = store.Save("stable-fps", 72, "visibility");
        var view = new GoalSettingsView(store);
        Field(view, "TargetText").Text = target;
        Field(view, "GoalText").Text = goal;
        Field(view, "QualityText").Text = quality;
        ((Button)view.FindName("SaveGoalButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(before, store.Load());
        Assert.NotEmpty(((TextBlock)view.FindName("GoalStatusText")).Text);
    });

    [Theory]
    [InlineData(1.5)]
    [InlineData(2)]
    public void GoalEditorFitsNarrowScaledSettingsAndSaveIsReachableByScrolling(double scale) => host.Run(() =>
    {
        var view = new GoalSettingsView(Store());
        Field(view, "QualityText").Text = new string('x', 240);
        var scroll = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var window = new Window { Content = scroll };
        try
        {
            WpfHost.Layout(window, 380, 260);
            Assert.True(scroll.ScrollableHeight > 0);
            Assert.True(Field(view, "QualityText").ActualWidth < 380);
            scroll.ScrollToBottom();
            WpfHost.Layout(window, 380, 260);
            var button = (Button)view.FindName("SaveGoalButton");
            var position = button.TranslatePoint(new Point(), scroll);
            Assert.InRange(position.Y, 0, 260 - button.ActualHeight);
            var bitmap = new RenderTargetBitmap((int)(380 * scale), (int)(260 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(scroll);
            Assert.Equal((int)(380 * scale), bitmap.PixelWidth);
        }
        finally { window.Close(); }
    });

    private static TextBox Field(GoalSettingsView view, string name) => Assert.IsType<TextBox>(view.FindName(name));
}
