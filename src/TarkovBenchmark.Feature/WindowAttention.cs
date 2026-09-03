using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TarkovBenchmark.Feature;

internal static class WindowAttention
{
    private const uint FlashTray = 0x00000002;
    private const uint FlashTimerNoForeground = 0x0000000C;

    public static async Task NotifyAsync(Window? window)
    {
        SystemSounds.Asterisk.Play();
        if (window is not null)
        {
            var info = new FlashWindowInfo { Size = (uint)Marshal.SizeOf<FlashWindowInfo>(), Window = new WindowInteropHelper(window).Handle, Flags = FlashTray | FlashTimerNoForeground, Count = 6, Timeout = 0 };
            FlashWindowEx(ref info);
        }
        await Task.Delay(350);
        SystemSounds.Asterisk.Play();
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo { public uint Size; public IntPtr Window; public uint Flags; public uint Count; public uint Timeout; }
}
