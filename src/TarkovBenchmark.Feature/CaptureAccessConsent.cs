using System.Windows;

namespace TarkovBenchmark.Feature;

internal static class CaptureAccessConsent
{
    internal static bool Show(Window? owner) => MessageBox.Show(owner,
        "Allow administrator setup and continue? Windows will ask for approval. Setup adds your account to Performance Log Users, a permission other apps can also use and which remains after uninstall. A temporary elevated collector lets you measure now without signing out. You can revoke access in Settings.",
        "Enable performance capture", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
