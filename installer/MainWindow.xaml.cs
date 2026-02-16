using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using File = System.IO.File;

namespace UYALauncherSetup;

public partial class MainWindow : Window {
    private string _installPath;

    // P/Invoke for creating shortcuts
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    public MainWindow() {
        InitializeComponent();
        
        // Check if embedded resources exist
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames();
        
        if (!resources.Any(r => r == "UYALauncher.exe")) {
            MessageBox.Show(
                "Error: UYALauncher.exe is not embedded in this installer.\n\n" +
                "Make sure you:\n" +
                "1. Built the launcher first (build-launcher.bat)\n" +
                "2. Then built the installer\n\n" +
                $"Found {resources.Length} embedded resources",
                "Installation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
            return;
        }
        
        // Ensure window shows on top
        Topmost = true;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = false; // Reset after showing
        
        // Default install path to Documents folder
        _installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "UYALauncher"
        );
        InstallPathTextBox.Text = _installPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e) {
        var dialog = new System.Windows.Forms.FolderBrowserDialog {
            Description = "Select installation folder",
            SelectedPath = _installPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
            _installPath = dialog.SelectedPath;
            InstallPathTextBox.Text = _installPath;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e) {
        _installPath = InstallPathTextBox.Text;

        if (string.IsNullOrWhiteSpace(_installPath)) {
            MessageBox.Show("Please select an installation location.", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Disable UI during installation
        InstallButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        InstallPathTextBox.IsEnabled = false;
        DesktopShortcutCheckBox.IsEnabled = false;

        // Show progress
        ProgressPanel.Visibility = Visibility.Visible;

        try {
            await Task.Run(() => PerformInstallation());

            // Create desktop shortcut if requested
            if (DesktopShortcutCheckBox.IsChecked == true) {
                CreateDesktopShortcut();
            }

            // Launch the installed application if requested
            if (RunAfterInstallCheckBox.IsChecked == true) {
                var launcherPath = Path.Combine(_installPath, "UYALauncher.exe");
                if (File.Exists(launcherPath)) {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = launcherPath,
                        UseShellExecute = true
                    });
                }
            }

            // Close installer
            Application.Current.Shutdown();
        } catch (Exception ex) {
            MessageBox.Show($"Installation failed:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            // Re-enable UI
            InstallButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            InstallPathTextBox.IsEnabled = true;
            DesktopShortcutCheckBox.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void PerformInstallation() {
        // Create installation directory
        Directory.CreateDirectory(_installPath);

        UpdateProgress("Extracting UYALauncher.exe...", 20);
        
        // Extract embedded UYALauncher.exe
        ExtractEmbeddedResource("UYALauncher.exe", Path.Combine(_installPath, "UYALauncher.exe"));

        UpdateProgress("Extracting data folder...", 50);
        
        // Extract all data folder resources
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        
        foreach (var resourceName in resourceNames) {
            // Extract data/ resources
            if (resourceName.StartsWith("data/") || resourceName.StartsWith("data\\")) {
                var relativePath = resourceName.Replace('/', Path.DirectorySeparatorChar);
                var outputPath = Path.Combine(_installPath, relativePath);
                
                // Create directory if needed
                var directory = Path.GetDirectoryName(outputPath);
                if (directory != null) {
                    Directory.CreateDirectory(directory);
                }
                
                // Extract file
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) {
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                }
            }
        }

        UpdateProgress("Installation complete", 100);
    }

    private void ExtractEmbeddedResource(string resourceName, string outputPath) {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null) {
            throw new Exception($"Could not find embedded resource: {resourceName}");
        }

        using var fileStream = File.Create(outputPath);
        stream.CopyTo(fileStream);
    }

    private void CreateDesktopShortcut() {
        try {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktopPath, "UYA Launcher.lnk");
            var targetPath = Path.Combine(_installPath, "UYALauncher.exe");

            // Create .lnk file using PowerShell
            var psCommand = $@"
                $WshShell = New-Object -ComObject WScript.Shell
                $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                $Shortcut.TargetPath = '{targetPath}'
                $Shortcut.WorkingDirectory = '{_installPath}'
                $Shortcut.Description = 'UYA Launcher'
                $Shortcut.Save()
            ";

            var psi = new System.Diagnostics.ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit();
        } catch (Exception ex) {
            // Non-fatal error
            Console.WriteLine($"Failed to create desktop shortcut: {ex.Message}");
        }
    }

    private void UpdateProgress(string text, int percent) {
        Dispatcher.Invoke(() => {
            ProgressText.Text = text;
            ProgressBar.Value = percent;
        });
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) {
        Application.Current.Shutdown();
    }
}
