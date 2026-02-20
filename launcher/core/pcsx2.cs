using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace UYALauncher;

public static class PCSX2Manager {
    private static Process? _pcsx2Process;
    private static IntPtr _pcsx2Window;
    private static CancellationTokenSource? _monitorCts;
    private static CancellationTokenSource? _sizeMonitorCts;
    private static int _lastWidth = 0;
    private static int _lastHeight = 0;

    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZE = 0x20000000;
    private const int WS_MAXIMIZE = 0x01000000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_CHILD = 0x40000000;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int SW_SHOW = 5;

    public static bool Launch(ConfigurationData config) {
        try {
            Console.WriteLine("=== PCSX2 Launch Debug ===");
            Console.WriteLine($"ISO Path: {config.IsoPath}");
            Console.WriteLine($"BIOS Path: {config.BiosPath}");
            
            // Use hardcoded PCSX2 path from data/emulator folder
            var pcsx2Path = Configuration.GetPcsx2Path();
            Console.WriteLine($"PCSX2 Path: {pcsx2Path}");
            Console.WriteLine($"ISO Exists: {System.IO.File.Exists(config.IsoPath)}");
            Console.WriteLine($"PCSX2 Exists: {System.IO.File.Exists(pcsx2Path)}");
            Console.WriteLine($"BIOS Exists: {System.IO.File.Exists(config.BiosPath)}");

            // Check if files exist
            if (!System.IO.File.Exists(config.IsoPath)) {
                MessageBox.Show(
                    $"ISO file not found:\n{config.IsoPath}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!System.IO.File.Exists(pcsx2Path)) {
                MessageBox.Show(
                    $"PCSX2 executable not found:\n{pcsx2Path}\n\nMake sure the data/emulator folder exists with pcsx2-qt.exe",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            
            if (!System.IO.File.Exists(config.BiosPath)) {
                MessageBox.Show(
                    $"BIOS file not found:\n{config.BiosPath}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var arguments = "";

            // Add portable mode flag
            arguments += " -portable";

            // Set up PCSX2 portable configuration
            try {
                var pcsx2Dir = System.IO.Path.GetDirectoryName(pcsx2Path);
                if (pcsx2Dir == null) {
                    Console.WriteLine("Warning: Could not get PCSX2 directory");
                    return true; // Continue anyway, PCSX2 might still work
                }
                
                var iniDir = System.IO.Path.Combine(pcsx2Dir, "inis");
                var iniPath = System.IO.Path.Combine(iniDir, "PCSX2.ini");
                
                // Parse BIOS path into folder and filename
                var biosFolder = System.IO.Path.GetDirectoryName(config.BiosPath) ?? "";
                var biosFilename = System.IO.Path.GetFileName(config.BiosPath);
                
                Console.WriteLine($"Parsed BIOS - Folder: {biosFolder}, File: {biosFilename}");
                
                // Create necessary directories
                System.IO.Directory.CreateDirectory(iniDir);
                
                // Create or update PCSX2.ini
                if (!System.IO.File.Exists(iniPath)) {
                    // Create default config
                    var defaultConfig = System.IO.Path.Combine(Configuration.GetAppDirectory(), "data", "defaults", "PCSX2.ini");
                    if (System.IO.File.Exists(defaultConfig)) {
                        System.IO.File.Copy(defaultConfig, iniPath, false);
                        Console.WriteLine("Created default PCSX2.ini");
                    }
                }
                
                // Update config with user's BIOS folder and filename
                if (System.IO.File.Exists(iniPath)) {
                    var lines = System.IO.File.ReadAllLines(iniPath);
                    bool inFolders = false;
                    bool inFilenames = false;
                    
                    for (int i = 0; i < lines.Length; i++) {
                        // Update [Folders] section - BIOS folder path
                        if (lines[i].Trim() == "[Folders]") {
                            inFolders = true;
                            inFilenames = false;
                        }
                        else if (inFolders && lines[i].StartsWith("Bios")) {
                            lines[i] = $"Bios = {biosFolder}";
                            Console.WriteLine($"Set BIOS folder to: {biosFolder}");
                            inFolders = false;
                        }
                        // Update [Filenames] section - BIOS filename
                        else if (lines[i].Trim() == "[Filenames]") {
                            inFilenames = true;
                            inFolders = false;
                        }
                        else if (inFilenames && lines[i].StartsWith("BIOS")) {
                            lines[i] = $"BIOS = {biosFilename}";
                            Console.WriteLine($"Set BIOS file to: {biosFilename}");
                            break;
                        }
                        else if (lines[i].StartsWith("[")) {
                            inFolders = false;
                            inFilenames = false;
                        }
                    }
                    System.IO.File.WriteAllLines(iniPath, lines);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Warning: Could not set up PCSX2 config: {ex.Message}");
            }

            // Faster boot for multiplayer
            if (config.Patches.BootToMultiplayer)
                arguments += " -fastboot";

            // Fullscreen mode
            if (config.Fullscreen)
                arguments += " -fullscreen";

            // Add ISO path
            arguments += $" -- \"{config.IsoPath}\"";

            Console.WriteLine("=== PCSX2 Launch Command ===");
            Console.WriteLine($"Executable: {pcsx2Path}");
            Console.WriteLine($"Arguments: {arguments}");
            Console.WriteLine($"Full Command: \"{pcsx2Path}\" {arguments}");
            Console.WriteLine("============================");

            var startInfo = new ProcessStartInfo {
                FileName = pcsx2Path,
                Arguments = arguments,
                UseShellExecute = true,  // Changed to true for better compatibility
                CreateNoWindow = false,
                WorkingDirectory = System.IO.Path.GetDirectoryName(pcsx2Path)
            };

            Console.WriteLine("Starting process...");
            _pcsx2Process = Process.Start(startInfo);

            if (_pcsx2Process == null) {
                MessageBox.Show(
                    "Failed to launch PCSX2. Process.Start returned null.\n\n" +
                    "This might happen if:\n" +
                    "- The PCSX2 path is incorrect\n" +
                    "- PCSX2 requires admin rights\n" +
                    "- The file is blocked by Windows",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            Console.WriteLine($"PCSX2 launched successfully (PID: {_pcsx2Process.Id})");
            return true;
        } catch (Exception ex) {
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            
            MessageBox.Show(
                $"Failed to launch PCSX2:\n\n{ex.Message}\n\n" +
                $"Error Type: {ex.GetType().Name}\n\n" +
                "Check the console output for more details.",
                "Launch Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Polls until PCSX2 has a visible top-level window, then forces it to the foreground.
    /// Works regardless of fullscreen or windowed mode.
    /// </summary>
    private static async Task FocusWhenReady() {
        for (int i = 0; i < 60; i++) { // up to 30 seconds
            await Task.Delay(500);

            if (_pcsx2Process == null || _pcsx2Process.HasExited) return;

            IntPtr target = FindFullscreenTopLevelWindow();
            if (target == IntPtr.Zero)
                target = FindPcsx2Window();

            if (target != IntPtr.Zero) {
                ShowWindow(target, SW_SHOW);
                SetForegroundWindow(target);
                Console.WriteLine($"PCSX2 window focused after {(i + 1) * 500}ms");
                return;
            }
        }
        Console.WriteLine("FocusWhenReady: timed out waiting for PCSX2 window");
    }

    public static async Task<bool> EmbedWindow(Window parentWindow, bool showConsole) {
        if (_pcsx2Process == null) {
            if (showConsole)
                Console.WriteLine("EmbedWindow: _pcsx2Process is null");
            return false;
        }

        if (showConsole)
            Console.WriteLine($"Waiting for PCSX2 window (Process ID: {_pcsx2Process.Id})...");

        // Wait for PCSX2 window to appear
        var maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++) {
            _pcsx2Window = FindPcsx2Window();
            if (_pcsx2Window != IntPtr.Zero) {
                if (showConsole)
                    Console.WriteLine($"Found PCSX2 window after {i + 1} attempts!");
                break;
            }

            if (showConsole && i % 5 == 0)
                Console.WriteLine($"Still waiting for PCSX2 window... attempt {i + 1}/{maxAttempts}");

            await Task.Delay(500);
        }

        if (_pcsx2Window == IntPtr.Zero) {
            if (showConsole)
                Console.WriteLine($"Could not find PCSX2 window after {maxAttempts} attempts.");

            MessageBox.Show(
                "Could not find PCSX2 window to embed.\n\n" +
                "PCSX2 is running but may appear in a separate window.",
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        try {
            if (showConsole)
                Console.WriteLine("Embedding PCSX2 window...");

            // Wait for PCSX2 to fully initialize its window before snapshotting size
            await Task.Delay(2000);

            // Get PCSX2 window size
            if (GetWindowRect(_pcsx2Window, out RECT pcsx2Rect)) {
                int pcsx2Width = pcsx2Rect.Right - pcsx2Rect.Left;
                int pcsx2Height = pcsx2Rect.Bottom - pcsx2Rect.Top;
                
                if (showConsole)
                    Console.WriteLine($"PCSX2 window size: {pcsx2Width}x{pcsx2Height}");

                // Resize parent window to match PCSX2 size
                parentWindow.Width = pcsx2Width;
                parentWindow.Height = pcsx2Height;
                
                if (showConsole)
                    Console.WriteLine($"Resized parent window to match PCSX2: {pcsx2Width}x{pcsx2Height}");
            }

            // Hide PCSX2 window before embedding
            const int SW_HIDE = 0;
            ShowWindow(_pcsx2Window, SW_HIDE);
            if (showConsole)
                Console.WriteLine("PCSX2 window hidden for embedding");

            // Get parent window handle
            var parentHandle = new WindowInteropHelper(parentWindow).Handle;
            if (showConsole)
                Console.WriteLine($"Parent window handle: {parentHandle}");

            // Remove PCSX2 window border and make it a child
            var style = GetWindowLong(_pcsx2Window, GWL_STYLE);
            if (showConsole)
                Console.WriteLine($"Original PCSX2 window style: 0x{style:X}");

            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU);
            style |= WS_CHILD;
            SetWindowLong(_pcsx2Window, GWL_STYLE, style);

            if (showConsole)
                Console.WriteLine($"New PCSX2 window style: 0x{style:X}");

            // Set parent
            var result = SetParent(_pcsx2Window, parentHandle);
            if (showConsole)
                Console.WriteLine($"SetParent result: {result}");

            // Resize PCSX2 to fill parent window (now that parent is sized correctly)
            var width = (int)parentWindow.Width;
            var height = (int)parentWindow.Height;
            if (showConsole)
                Console.WriteLine($"Sizing embedded PCSX2 to fill parent: {width}x{height}");

            SetWindowPos(_pcsx2Window, IntPtr.Zero, 0, 0, 
                        width, height,
                        SWP_NOZORDER | SWP_FRAMECHANGED);

            if (showConsole)
                Console.WriteLine("PCSX2 window sized, now showing...");

            // Show PCSX2 window (now embedded and properly sized)
            ShowWindow(_pcsx2Window, SW_SHOW);

            if (showConsole)
                Console.WriteLine("PCSX2 window embedded and shown successfully!");

            // Fire-and-forget: keep polling until PCSX2 has a focusable window and push focus to it.
            // This handles both windowed and fullscreen without needing a fixed delay.
            _ = FocusWhenReady();

            return true;
        } catch (Exception ex) {
            if (showConsole)
                Console.WriteLine($"Error embedding window: {ex.Message}");
            return false;
        }
    }

    private static IntPtr FindPcsx2Window() {
        if (_pcsx2Process == null)
            return IntPtr.Zero;

        IntPtr foundWindow = IntPtr.Zero;
        var processId = (uint)_pcsx2Process.Id;

        EnumWindows((hWnd, lParam) => {
            GetWindowThreadProcessId(hWnd, out uint windowProcessId);

            if (windowProcessId == processId && IsWindowVisible(hWnd)) {
                var className = new System.Text.StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                var classStr = className.ToString();

                // Filter out console and IME windows
                if (classStr != "ConsoleWindowClass" && classStr != "IME") {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindow;
    }

    /// <summary>
    /// Finds the fullscreen top-level window belonging to PCSX2.
    /// PCSX2 Qt creates a separate borderless popup for exclusive fullscreen,
    /// distinct from the embedded child window.
    /// </summary>
    private static IntPtr FindFullscreenTopLevelWindow() {
        if (_pcsx2Process == null) return IntPtr.Zero;

        int screenWidth  = (int)SystemParameters.PrimaryScreenWidth;
        int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        var processId = (uint)_pcsx2Process.Id;
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, lParam) => {
            GetWindowThreadProcessId(hWnd, out uint windowProcessId);
            if (windowProcessId != processId) return true;
            if (!IsWindowVisible(hWnd)) return true;

            // Must be a top-level (not child) window
            int style = GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_CHILD) != 0) return true;

            if (GetWindowRect(hWnd, out RECT rect)) {
                int w = rect.Right  - rect.Left;
                int h = rect.Bottom - rect.Top;
                if (w >= screenWidth - 50 && h >= screenHeight - 50) {
                    found = hWnd;
                    return false; // stop enumerating
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>
    /// Checks whether PCSX2 currently has a fullscreen top-level window.
    /// Uses top-level window enumeration rather than the embedded child rect,
    /// since the child rect is parent-relative after SetParent and cannot be
    /// compared to screen dimensions.
    /// </summary>
    private static bool HasFullscreenTopLevelWindow() {
        return FindFullscreenTopLevelWindow() != IntPtr.Zero;
    }

    public static void StartMonitoring(Window? parentWindow, bool showConsole) {
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        Task.Run(async () => {
            if (showConsole)
                Console.WriteLine("PCSX2 process monitor started.");

            try {
                while (!token.IsCancellationRequested && _pcsx2Process != null) {
                    if (_pcsx2Process.HasExited) {
                        if (showConsole)
                            Console.WriteLine("PCSX2 process has exited.");

                        // Shut down the entire application on UI thread
                        Application.Current.Dispatcher.Invoke(() => {
                            if (showConsole)
                                Console.WriteLine("Shutting down application...");
                            Environment.Exit(0); // Force quit everything including console
                        });

                        break;
                    }

                    await Task.Delay(1000, token);
                }
            } catch (OperationCanceledException) {
                // Normal cancellation
            }

            if (showConsole)
                Console.WriteLine("PCSX2 process monitor stopped.");
        }, token);
    }

    public static void Terminate() {
        try {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;

            _sizeMonitorCts?.Cancel();
            _sizeMonitorCts?.Dispose();
            _sizeMonitorCts = null;

            if (_pcsx2Process != null && !_pcsx2Process.HasExited) {
                _pcsx2Process.Kill();
                _pcsx2Process.WaitForExit(2000);
            }

            _pcsx2Process?.Dispose();
            _pcsx2Process = null;
            _pcsx2Window = IntPtr.Zero;
        } catch {
            // Ignore cleanup errors
        }
    }

    public static void ResizeEmbeddedWindow(double width, double height) {
        if (_pcsx2Window != IntPtr.Zero) {
            SetWindowPos(_pcsx2Window, IntPtr.Zero, 0, 0, (int)width, (int)height, SWP_NOZORDER);
        }
    }

    public static void FocusEmbeddedWindow() {
        if (_pcsx2Window != IntPtr.Zero) {
            SetForegroundWindow(_pcsx2Window);
            Console.WriteLine("Focused embedded PCSX2 window");
        }
    }

    public static async Task FocusPCSX2Window() {
        if (_pcsx2Process == null) {
            Console.WriteLine("Cannot focus PCSX2 - process is null");
            return;
        }

        for (int i = 0; i < 10; i++) {
            // Prefer the fullscreen top-level if it exists, fall back to any window
            IntPtr target = FindFullscreenTopLevelWindow();
            if (target == IntPtr.Zero)
                target = FindPcsx2Window();

            if (target != IntPtr.Zero) {
                ShowWindow(target, SW_SHOW);
                SetForegroundWindow(target);
                Console.WriteLine($"PCSX2 window focused (attempt {i + 1})");
                return;
            }

            Console.WriteLine($"Could not find PCSX2 window yet, attempt {i + 1}/10");
            await Task.Delay(500);
        }

        Console.WriteLine("Failed to focus PCSX2 window after 10 attempts");
    }

    public static bool IsPCSX2Fullscreen() {
        return HasFullscreenTopLevelWindow();
    }

    public static void StartSizeMonitoring(Window parentWindow) {
        _sizeMonitorCts = new CancellationTokenSource();
        var token = _sizeMonitorCts.Token;

        Task.Run(async () => {
            Console.WriteLine("PCSX2 window size monitor started.");
            bool? lastFullscreenState = null; // null = unknown, forces first-tick evaluation

            try {
                while (!token.IsCancellationRequested && _pcsx2Process != null && !_pcsx2Process.HasExited) {
                    await Task.Delay(500, token);

                    // Detect fullscreen via top-level window enumeration.
                    // We cannot use the embedded child's rect because after SetParent
                    // it is parent-relative, not screen-relative.
                    bool isFullscreen = HasFullscreenTopLevelWindow();

                    // Only act on state transitions
                    if (isFullscreen == lastFullscreenState) continue;
                    lastFullscreenState = isFullscreen;

                    Console.WriteLine($"PCSX2 fullscreen state changed: {isFullscreen}");

                    if (isFullscreen) {
                        Application.Current.Dispatcher.Invoke(() => {
                            Console.WriteLine("PCSX2 entered fullscreen - hiding parent window");
                            parentWindow.Hide();
                            parentWindow.WindowState = WindowState.Minimized;
                        });
                    } else {
                        Application.Current.Dispatcher.Invoke(() => {
                            Console.WriteLine("PCSX2 exited fullscreen - showing parent window");
                            parentWindow.Show();
                            parentWindow.WindowState = WindowState.Normal;
                            parentWindow.Activate();
                            ResizeEmbeddedWindow(parentWindow.ActualWidth, parentWindow.ActualHeight);
                        });
                    }
                }
            } catch (OperationCanceledException) {
                // Normal cancellation
            }

            Console.WriteLine("PCSX2 window size monitor stopped.");
        }, token);
    }
}