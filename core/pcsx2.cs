using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace UYALauncher;

public static class PCSX2Manager
{
    private static Process? _pcsx2Process;
    private static IntPtr _pcsx2Window;
    private static CancellationTokenSource? _monitorCts;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
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

    public static bool Launch(ConfigurationData config)
    {
        try
        {
            Console.WriteLine("=== PCSX2 Launch Debug ===");
            Console.WriteLine($"ISO Path: {config.IsoPath}");
            Console.WriteLine($"PCSX2 Path: {config.Pcsx2Path}");
            Console.WriteLine($"ISO Exists: {System.IO.File.Exists(config.IsoPath)}");
            Console.WriteLine($"PCSX2 Exists: {System.IO.File.Exists(config.Pcsx2Path)}");

            // Check if files exist
            if (!System.IO.File.Exists(config.IsoPath))
            {
                MessageBox.Show(
                    $"ISO file not found:\n{config.IsoPath}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!System.IO.File.Exists(config.Pcsx2Path))
            {
                MessageBox.Show(
                    $"PCSX2 executable not found:\n{config.Pcsx2Path}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var arguments = "";

            // Faster boot for multiplayer
            if (config.Patches.BootToMultiplayer)
                arguments += " -fastboot";

            // Fullscreen mode
            if (config.Fullscreen)
                arguments += " -fullscreen";

            // Add ISO path
            arguments += $" -- \"{config.IsoPath}\"";

            Console.WriteLine($"Command: \"{config.Pcsx2Path}\" {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = config.Pcsx2Path,
                Arguments = arguments,
                UseShellExecute = true,  // Changed to true for better compatibility
                CreateNoWindow = false,
                WorkingDirectory = System.IO.Path.GetDirectoryName(config.Pcsx2Path)
            };

            Console.WriteLine("Starting process...");
            _pcsx2Process = Process.Start(startInfo);

            if (_pcsx2Process == null)
            {
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
        }
        catch (Exception ex)
        {
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

    public static async Task<bool> EmbedWindow(Window parentWindow, bool showConsole)
    {
        if (_pcsx2Process == null)
        {
            if (showConsole)
                Console.WriteLine("EmbedWindow: _pcsx2Process is null");
            return false;
        }

        if (showConsole)
            Console.WriteLine($"Waiting for PCSX2 window (Process ID: {_pcsx2Process.Id})...");

        // Wait for PCSX2 window to appear
        var maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++)
        {
            _pcsx2Window = FindPcsx2Window();
            if (_pcsx2Window != IntPtr.Zero)
            {
                if (showConsole)
                    Console.WriteLine($"Found PCSX2 window after {i + 1} attempts!");
                break;
            }

            if (showConsole && i % 5 == 0)
                Console.WriteLine($"Still waiting for PCSX2 window... attempt {i + 1}/{maxAttempts}");

            await Task.Delay(500);
        }

        if (_pcsx2Window == IntPtr.Zero)
        {
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

        try
        {
            if (showConsole)
                Console.WriteLine("Embedding PCSX2 window...");

            // Get PCSX2 window size
            if (GetWindowRect(_pcsx2Window, out RECT pcsx2Rect))
            {
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
            var width = (int)parentWindow.ActualWidth;
            var height = (int)parentWindow.ActualHeight;
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

            return true;
        }
        catch (Exception ex)
        {
            if (showConsole)
                Console.WriteLine($"Error embedding window: {ex.Message}");
            return false;
        }
    }

    private static IntPtr FindPcsx2Window()
    {
        if (_pcsx2Process == null)
            return IntPtr.Zero;

        IntPtr foundWindow = IntPtr.Zero;
        var processId = (uint)_pcsx2Process.Id;

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint windowProcessId);

            if (windowProcessId == processId && IsWindowVisible(hWnd))
            {
                var className = new System.Text.StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                var classStr = className.ToString();

                // Filter out console and IME windows
                if (classStr != "ConsoleWindowClass" && classStr != "IME")
                {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindow;
    }

    public static void StartMonitoring(Window? parentWindow, bool showConsole)
    {
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        Task.Run(async () =>
        {
            if (showConsole)
                Console.WriteLine("PCSX2 process monitor started.");

            try
            {
                while (!token.IsCancellationRequested && _pcsx2Process != null)
                {
                    if (_pcsx2Process.HasExited)
                    {
                        if (showConsole)
                            Console.WriteLine("PCSX2 process has exited.");

                        // Close parent window on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            parentWindow?.Close();
                        });

                        break;
                    }

                    await Task.Delay(1000, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }

            if (showConsole)
                Console.WriteLine("PCSX2 process monitor stopped.");
        }, token);
    }

    public static void Terminate()
    {
        try
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;

            if (_pcsx2Process != null && !_pcsx2Process.HasExited)
            {
                _pcsx2Process.Kill();
                _pcsx2Process.WaitForExit(2000);
            }

            _pcsx2Process?.Dispose();
            _pcsx2Process = null;
            _pcsx2Window = IntPtr.Zero;
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public static void ResizeEmbeddedWindow(double width, double height)
    {
        if (_pcsx2Window != IntPtr.Zero)
        {
            SetWindowPos(_pcsx2Window, IntPtr.Zero, 0, 0,
                        (int)width, (int)height,
                        SWP_NOZORDER);
        }
    }
}
