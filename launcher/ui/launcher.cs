using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace UYALauncher;

public class LauncherWindow : Window {
    private readonly ConfigurationData _config;
    private const int HOTKEY_F11 = 9001;
    private const int HOTKEY_CTRL_F11 = 9002;
    private static SettingsWindow? _openSettingsWindow = null;

    // Win32 API for global hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_NONE = 0x0000;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_F11 = 0x7A;

    public LauncherWindow(ConfigurationData config) {
        _config = config;
        Title = "UYA Launcher";
        Width = 960;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        
        // Start minimized if embedding is enabled (will restore after embedding completes)
        if (_config.EmbedWindow) {
            WindowState = WindowState.Minimized;
            Console.WriteLine("Window initially minimized for embedding");
        }
        
        // Apply theme (high contrast or dark)
        ThemeHelper.ApplyTheme(this);

        // Handle resize to resize embedded window
        SizeChanged += (s, e) => {
            PCSX2Manager.ResizeEmbeddedWindow(ActualWidth, ActualHeight);
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) {
        // Register global hotkeys after window handle is created
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;

        Console.WriteLine($"=== OnSourceInitialized ===");
        Console.WriteLine($"Window handle: {handle}");
        Console.WriteLine($"Window visible: {IsVisible}");

        if (handle != IntPtr.Zero) {
            // Register F11 for map updates
            bool f11Success = RegisterHotKey(handle, HOTKEY_F11, MOD_NONE, VK_F11);
            Console.WriteLine($"Registering F11 hotkey (ID: {HOTKEY_F11}): {f11Success}");
            if (!f11Success) {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Console.WriteLine($"F11 registration failed with error code: {error}");
                if (error == 1409)
                    Console.WriteLine("Error 1409 = Hot key is already registered");
            }

            // Register Ctrl+F11 for settings
            bool ctrlF11Success = RegisterHotKey(handle, HOTKEY_CTRL_F11, MOD_CONTROL, VK_F11);
            Console.WriteLine($"Registering Ctrl+F11 hotkey (ID: {HOTKEY_CTRL_F11}): {ctrlF11Success}");
            if (!ctrlF11Success) {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Console.WriteLine($"Ctrl+F11 registration failed with error code: {error}");
                if (error == 1409)
                    Console.WriteLine("Error 1409 = Hot key is already registered");
            }

            // Add hook to process Windows messages
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null) {
                source.AddHook(WndProc);
                Console.WriteLine("WndProc message hook added successfully");
                Console.WriteLine("Hotkeys should now be active - try pressing F11 or Ctrl+F11");
            } else {
                Console.WriteLine("ERROR: Could not get HwndSource!");
            }
        } else {
            Console.WriteLine("ERROR: Window handle is zero!");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY) {
            int hotkeyId = wParam.ToInt32();
            Console.WriteLine($"WM_HOTKEY message received! Hotkey ID: {hotkeyId}");

            switch (hotkeyId) {
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

    private async Task UpdateMapsAsync() {
        if (!string.IsNullOrEmpty(_config.IsoPath)) {
            try {
                await MapUpdater.UpdateMapsAsync(_config.IsoPath, _config.Region);
                Console.WriteLine("Map update completed via hotkey");
            } catch (Exception ex) {
                Console.WriteLine($"Map update error: {ex.Message}");
            }
        }
    }

    private void OpenSettings() {
        // If settings window is already open, just focus it
        if (_openSettingsWindow != null) {
            Console.WriteLine("Settings window already open - bringing to front");
            
            // Force to top and activate
            _openSettingsWindow.Topmost = true;
            _openSettingsWindow.Activate();
            _openSettingsWindow.Focus();
            _openSettingsWindow.Topmost = false; // Reset so it doesn't stay always on top
            
            // If minimized, restore it
            if (_openSettingsWindow.WindowState == WindowState.Minimized) {
                _openSettingsWindow.WindowState = WindowState.Normal;
            }
            
            return;
        }

        Console.WriteLine("Opening new settings window");
        var settingsWindow = new SettingsWindow(hotkeyMode: true);
        _openSettingsWindow = settingsWindow;
        
        // Clear reference when window closes
        settingsWindow.Closed += (s, e) => {
            Console.WriteLine("Settings window closed");
            _openSettingsWindow = null;
        };
        
        // Make it topmost temporarily to ensure it appears above fullscreen
        settingsWindow.Topmost = true;
        settingsWindow.Show();
        settingsWindow.Activate();
        settingsWindow.Topmost = false; // Reset after showing
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) {
        Console.WriteLine("=== LauncherWindow.OnLoaded ===");
        Console.WriteLine($"ISO Path: '{_config.IsoPath}'");
        Console.WriteLine($"BIOS Path: '{_config.BiosPath}'");
        Console.WriteLine($"PCSX2 Path: '{Configuration.GetPcsx2Path()}'");
        Console.WriteLine($"Region: '{_config.Region}'");
        Console.WriteLine($"EmbedWindow: {_config.EmbedWindow}");
        Console.WriteLine($"ShowConsole: {_config.ShowConsole}");
        
        // Update maps first if ISO is configured
        if (!string.IsNullOrEmpty(_config.IsoPath)) {
            Console.WriteLine("Checking for custom map updates...");
            try {
                await MapUpdater.UpdateMapsAsync(_config.IsoPath, _config.Region);
                Console.WriteLine("Map update completed");
            } catch (Exception ex) {
                Console.WriteLine($"Map update error: {ex.Message}");
            }
        } else {
            Console.WriteLine("No ISO path configured, skipping map updates");
        }

        // Apply patches AFTER map updates, BEFORE launching PCSX2
        Console.WriteLine("Applying patches...");
        try {
            PatchManager.ApplyPatches(_config);
            Console.WriteLine("Patches applied successfully");
        } catch (Exception ex) {
            Console.WriteLine($"Patch error: {ex.Message}");
        }

        // Launch PCSX2
        Console.WriteLine("Attempting to launch PCSX2...");
        if (!PCSX2Manager.Launch(_config)) {
            Console.WriteLine("PCSX2 launch failed!");
            Close();
            return;
        }
        Console.WriteLine("PCSX2 launch succeeded!");

        // Embed PCSX2 window if configured
        if (_config.EmbedWindow) {
            Console.WriteLine("Embed mode enabled - waiting for PCSX2 window to appear...");
            
            var embedded = await PCSX2Manager.EmbedWindow(this, true);
            Console.WriteLine($"Embed result: {embedded}");

            if (!embedded) {
                Console.WriteLine("WARNING: Embedding failed, but PCSX2 is running in separate window");
                // Show window if embedding failed
                Visibility = Visibility.Visible;
                WindowState = WindowState.Normal;
                Activate();
            } else {
                Console.WriteLine("Embedding successful!");

                // Use the config flag to decide initial visibility.
                // We cannot rely on IsPCSX2Fullscreen() here because after SetParent
                // the child rect is parent-relative, not screen-relative.
                if (!_config.Fullscreen) {
                    Console.WriteLine("PCSX2 is windowed - showing parent window");
                    Visibility = Visibility.Visible;
                    WindowState = WindowState.Normal;
                    Activate();
                } else {
                    Console.WriteLine("PCSX2 launched fullscreen - keeping parent window hidden");

                    // Give PCSX2 time to fully enter fullscreen, then push focus to its
                    // top-level fullscreen window (the parent can't receive SetForegroundWindow
                    // while hidden, and the embedded child ignores it entirely).
                    _ = Task.Delay(1500).ContinueWith(_ => {
                        Application.Current.Dispatcher.Invoke(async () => {
                            await PCSX2Manager.FocusPCSX2Window();
                            Console.WriteLine("Pushed focus to PCSX2 fullscreen window after delay");
                        });
                    });
                }

                // Start monitoring PCSX2 window size/fullscreen changes AFTER
                // initial visibility is decided.
                PCSX2Manager.StartSizeMonitoring(this);
            }
        } else {
            Console.WriteLine("Embed mode disabled, PCSX2 will run in separate window");
            
            // Wait for PCSX2 window to appear, then focus it
            await Task.Delay(1000);
            await PCSX2Manager.FocusPCSX2Window();
        }

        // Start monitoring PCSX2 process
        Console.WriteLine("Starting PCSX2 process monitor...");
        PCSX2Manager.StartMonitoring(this, true);
        Console.WriteLine("LauncherWindow fully loaded and ready");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) {
        Console.WriteLine("Launcher window closing...");

        // Unregister hotkeys
        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;
        if (handle != IntPtr.Zero) {
            UnregisterHotKey(handle, HOTKEY_F11);
            UnregisterHotKey(handle, HOTKEY_CTRL_F11);
            Console.WriteLine("Hotkeys unregistered");
        }
        
        // Terminate PCSX2 and exit
        PCSX2Manager.Terminate();
        Console.WriteLine("Exiting application...");
        Environment.Exit(0);
    }
}