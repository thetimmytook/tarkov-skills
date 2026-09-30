using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace TarkovBenchmark.Feature;

public static class SkillsGuide
{
    public const string Url = "https://github.com/thetimmytook/tarkov-skills#install";

    public static void Open(Window owner)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(owner, $"Could not open your browser. Open this address to set up the skills:\n\n{Url}",
                "Skills setup guide", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
