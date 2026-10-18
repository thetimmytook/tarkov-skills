using System.Windows;
using System.Windows.Controls;
using TarkovSkills.Core.Permissions;

namespace TarkovBenchmark.Feature;

[Flags]
public enum SettingsSections { None = 0, Access = 1, Goal = 2 }

public partial class SettingsView : UserControl
{
    public SettingsSections Sections { get; }

    public SettingsView(CaptureAccessSession session, SettingsSections sections = SettingsSections.Access)
        : this(sections, () => new CaptureAccessView(session), () => new GoalSettingsView()) { }

    internal SettingsView(SettingsSections sections, Func<UserControl> access, Func<UserControl> goal)
    {
        if ((sections & ~(SettingsSections.Access | SettingsSections.Goal)) != 0)
            throw new ArgumentOutOfRangeException(nameof(sections));
        InitializeComponent();
        Sections = sections;
        if (sections.HasFlag(SettingsSections.Access))
        {
            AccessSection.Content = access();
            AccessSection.Visibility = Visibility.Visible;
        }
        if (sections.HasFlag(SettingsSections.Goal))
        {
            GoalSection.Content = goal();
            GoalSection.Visibility = Visibility.Visible;
            if (sections.HasFlag(SettingsSections.Access)) GoalSection.Margin = new Thickness(0, 16, 0, 0);
        }
    }
}
