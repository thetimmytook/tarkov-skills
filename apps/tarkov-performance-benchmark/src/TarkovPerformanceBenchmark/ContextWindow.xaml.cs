using System.Windows;
using System.Windows.Controls;

namespace TarkovPerformanceBenchmark;

public partial class ContextWindow : Window
{
    private readonly RaidContext _context;
    public ContextAnswers? Answers { get; private set; }
    public ContextWindow(RaidContext context)
    {
        InitializeComponent(); _context = context; DetectedMapText.Text = context.Map == "unknown" ? "Map could not be identified from Tarkov logs." : $"Map: {context.Map}";
        WeatherCombo.ItemsSource = new[] { "Not sure", "Clear", "Cloudy", "Rain", "Fog", "Snow" }; WeatherCombo.SelectedIndex = 0;
        TimeCombo.ItemsSource = new[] { "Not sure", "Day", "Night", "Dawn / dusk" }; TimeCombo.SelectedIndex = 0;
        if (context.Map == "unknown") { MapPanel.Visibility = Visibility.Visible; MapCombo.ItemsSource = new[] { "Streets of Tarkov", "Customs", "Factory", "The Lab", "Lighthouse", "Reserve", "Ground Zero", "Interchange", "Shoreline", "Woods", "Labyrinth" }; }
    }
    private void RequiredField_Changed(object sender, RoutedEventArgs e) => UpdateSave();
    private void RequiredField_Changed(object sender, SelectionChangedEventArgs e) => UpdateSave();
    private void UpdateSave() { if (SaveButton is null) return; SaveButton.IsEnabled = (BsgRadio.IsChecked == true || LocalRadio.IsChecked == true) && (_context.Map != "unknown" || MapCombo.SelectedItem is not null); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var weather = new Dictionary<string, string> { ["Not sure"] = "unknown", ["Clear"] = "clear", ["Cloudy"] = "cloudy", ["Rain"] = "rain", ["Fog"] = "fog", ["Snow"] = "snow" };
        var time = new Dictionary<string, string> { ["Not sure"] = "unknown", ["Day"] = "day", ["Night"] = "night", ["Dawn / dusk"] = "dawn_dusk" };
        Answers = new(BsgRadio.IsChecked == true ? "bsg_servers" : "local", weather[(string)WeatherCombo.SelectedItem], time[(string)TimeCombo.SelectedItem], _context.Map == "unknown" ? (string)MapCombo.SelectedItem : _context.Map); DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
