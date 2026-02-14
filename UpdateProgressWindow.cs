using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace UYALauncher;

public class UpdateProgressWindow : Window
{
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;

    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public UpdateProgressWindow()
    {
        Title = "Downloading Update";
        Width = 450;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        
        // Dark theme background
        Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
        Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        // Enable dark title bar
        Loaded += (s, e) => SetDarkTitleBar();

        var grid = new Grid();
        grid.Margin = new Thickness(20);

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

        _statusText = new TextBlock
        {
            Text = "Preparing download...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            FontSize = 14
        };
        Grid.SetRow(_statusText, 0);
        grid.Children.Add(_statusText);

        _progressBar = new ProgressBar
        {
            Height = 24,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)), // Accent blue
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(1)
        };
        Grid.SetRow(_progressBar, 2);
        grid.Children.Add(_progressBar);

        Content = grid;
    }

    private void SetDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
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

    public void UpdateProgress(int percent)
    {
        _progressBar.Value = percent;
        _statusText.Text = $"Downloading update... {percent}%";

        if (percent >= 100)
        {
            _statusText.Text = "Download complete!";
        }
    }
}
