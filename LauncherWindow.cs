using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace UYALauncher;

public class LauncherWindow : Window
{
    private readonly ConfigurationData _config;
    private const int HOTKEY_F11 = 9001;
    private const int HOTKEY_CTRL_F11 = 9002;

    // Win32 API for global hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const uint MOD_NONE = 0x0000;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_F11 = 0x7A;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public LauncherWindow(ConfigurationData config)
    {
        _config = config;
        Title = "UYA Launcher";
        Width = 960;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        
        // Dark theme background
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));
        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));

        // Handle resize to resize embedded window
        SizeChanged += (s, e) =>
        {
            PCSX2Manager.ResizeEmbeddedWindow(ActualWidth, ActualHeight);
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Register global hotkeys after window handle is created
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;

        Console.WriteLine($"Window handle for hotkeys: {handle}");

        if (handle != IntPtr.Zero)
        {
            // Enable dark title bar
            try
            {
                int value = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
                Console.WriteLine("Dark title bar enabled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set dark title bar: {ex.Message}");
            }

            // Register F11 for map updates
            bool f11Success = RegisterHotKey(handle, HOTKEY_F11, MOD_NONE, VK_F11);
            Console.WriteLine($"Registering F11 hotkey (ID: {HOTKEY_F11}): {f11Success}");
            if (!f11Success)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"F11 registration failed with error: {error}");
            }

            // Register Ctrl+F11 for settings
            bool ctrlF11Success = RegisterHotKey(handle, HOTKEY_CTRL_F11, MOD_CONTROL, VK_F11);
            Console.WriteLine($"Registering Ctrl+F11 hotkey (ID: {HOTKEY_CTRL_F11}): {ctrlF11Success}");
            if (!ctrlF11Success)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Ctrl+F11 registration failed with error: {error}");
            }

            // Add hook to process Windows messages
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(WndProc);
                Console.WriteLine("Message hook added successfully");
            }
            else
            {
                Console.WriteLine("ERROR: Could not get HwndSource!");
            }
        }
        else
        {
            Console.WriteLine("ERROR: Window handle is zero!");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            Console.WriteLine($"WM_HOTKEY message received! Hotkey ID: {hotkeyId}");

            switch (hotkeyId)
            {
                case HOTKEY_F11:
                    Console.WriteLine("F11 hotkey triggered: Updating maps...");
                    _ = UpdateMapsAsync();
                    handled = true;
                    break;

                case HOTKEY_CTRL_F11:
                    Console.WriteLine("Ctrl+F11 hotkey triggered: Opening settings...");
                    OpenSettings();
                    handled = true;
                    break;

                default:
                    Console.WriteLine($"Unknown hotkey ID: {hotkeyId}");
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private async Task UpdateMapsAsync()
    {
        if (!string.IsNullOrEmpty(_config.IsoPath))
        {
            try
            {
                await MapUpdater.UpdateMapsAsync(_config.IsoPath, _config.Region);
                Console.WriteLine("Map update completed via hotkey");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Map update error: {ex.Message}");
            }
        }
    }

    private void OpenSettings()
    {
        var settingsWindow = new MainWindow(hotkeyMode: true);
        settingsWindow.ShowDialog();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("=== LauncherWindow.OnLoaded ===");
        Console.WriteLine($"ISO Path: '{_config.IsoPath}'");
        Console.WriteLine($"PCSX2 Path: '{_config.Pcsx2Path}'");
        Console.WriteLine($"Region: '{_config.Region}'");
        Console.WriteLine($"EmbedWindow: {_config.EmbedWindow}");
        Console.WriteLine($"ShowConsole: {_config.ShowConsole}");
        
        // Update maps first if ISO is configured
        if (!string.IsNullOrEmpty(_config.IsoPath))
        {
            Console.WriteLine("Checking for custom map updates...");
            try
            {
                await MapUpdater.UpdateMapsAsync(_config.IsoPath, _config.Region);
                Console.WriteLine("Map update completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Map update error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("No ISO path configured, skipping map updates");
        }

        // Launch PCSX2
        Console.WriteLine("Attempting to launch PCSX2...");
        if (!PCSX2Manager.Launch(_config))
        {
            Console.WriteLine("PCSX2 launch failed!");
            Close();
            return;
        }
        Console.WriteLine("PCSX2 launch succeeded!");

        // Embed PCSX2 window if configured
        if (_config.EmbedWindow)
        {
            Console.WriteLine("Embed mode enabled");
            Console.WriteLine("Waiting 2 seconds before attempting to embed...");
            
            // Wait a bit for PCSX2 to fully start
            await Task.Delay(2000);
            
            Console.WriteLine("Attempting to embed PCSX2 window...");
            var embedded = await PCSX2Manager.EmbedWindow(this, true);
            Console.WriteLine($"Embed result: {embedded}");

            if (!embedded)
            {
                Console.WriteLine("WARNING: Embedding failed, but PCSX2 is running in separate window");
            }
            else
            {
                Console.WriteLine("Embedding successful!");
            }
        }
        else
        {
            Console.WriteLine("Embed mode disabled, PCSX2 will run in separate window");
        }

        // Start monitoring PCSX2 process
        Console.WriteLine("Starting PCSX2 process monitor...");
        PCSX2Manager.StartMonitoring(this, true);
        Console.WriteLine("LauncherWindow fully loaded and ready");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Console.WriteLine("Launcher window closing...");

        // Unregister hotkeys
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HOTKEY_F11);
            UnregisterHotKey(handle, HOTKEY_CTRL_F11);
            Console.WriteLine("Hotkeys unregistered");
        }

        PCSX2Manager.Terminate();
    }
}
