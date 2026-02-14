using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace UYALauncher;

public static class ThemeHelper
{
    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    // Check if high contrast is enabled
    public static bool IsHighContrastEnabled()
    {
        return SystemParameters.HighContrast;
    }

    // Apply theme to a window
    public static void ApplyTheme(Window window)
    {
        if (IsHighContrastEnabled())
        {
            // Use system high contrast colors
            Console.WriteLine("High contrast mode detected - using system colors");
            window.Background = SystemColors.WindowBrush;
            window.Foreground = SystemColors.WindowTextBrush;
        }
        else
        {
            // Use dark theme
            window.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            window.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            
            // Enable dark title bar
            window.Loaded += (s, e) => SetDarkTitleBar(window);
        }
    }

    private static void SetDarkTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int value = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
        }
        catch
        {
            // Ignore errors
        }
    }
}
