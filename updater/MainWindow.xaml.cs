using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace UYALauncherUpdater;

public partial class MainWindow : Window {
    private string _installPath;
    private bool _launcherWasRunning = false;

    public MainWindow() {
        InitializeComponent();
        
        // Get install path (where the updater is running from)
        _installPath = AppDomain.CurrentDomain.BaseDirectory;
        
        // Start update process immediately
        Loaded += async (s, e) => await PerformUpdate();
    }

    private async Task PerformUpdate() {
        try {
            UpdateStatus("Checking for running processes...", 10);
            await Task.Delay(500);
            
            // Check if UYALauncher is running and close it
            var launcherPath = Path.Combine(_installPath, "UYALauncher.exe");
            var launcherProcesses = Process.GetProcessesByName("UYALauncher");
            
            if (launcherProcesses.Length > 0) {
                _launcherWasRunning = true;
                UpdateStatus("Closing UYA Launcher...", 20);
                
                foreach (var process in launcherProcesses) {
                    try {
                        process.Kill();
                        process.WaitForExit(5000);
                    } catch {
                        // Ignore errors
                    }
                }
                
                await Task.Delay(1000); // Wait for file handles to release
            }

            UpdateStatus("Extracting updates...", 40);
            await Task.Run(() => ExtractEmbeddedFiles());

            UpdateStatus("Update complete!", 100);
            await Task.Delay(500);

            // Relaunch UYA Launcher if it was running
            if (_launcherWasRunning && File.Exists(launcherPath)) {
                Process.Start(new ProcessStartInfo {
                    FileName = launcherPath,
                    UseShellExecute = true,
                    WorkingDirectory = _installPath
                });
            }

            // Close updater
            Application.Current.Shutdown();
        } catch (Exception ex) {
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Update Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void ExtractEmbeddedFiles() {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames) {
            // Determine output path based on resource name
            string outputPath;
            
            if (resourceName == "UYALauncher.exe") {
                // Special handling for launcher exe - save as temp, will replace after
                outputPath = Path.Combine(_installPath, "UYALauncher.exe.new");
            } else if (resourceName.StartsWith("data/") || resourceName.StartsWith("data\\")) {
                // Data folder resources
                var relativePath = resourceName.Replace('/', Path.DirectorySeparatorChar);
                outputPath = Path.Combine(_installPath, relativePath);
            } else {
                // Skip unknown resources
                continue;
            }

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

        // Replace launcher exe if we extracted a new one
        var newLauncherPath = Path.Combine(_installPath, "UYALauncher.exe.new");
        var oldLauncherPath = Path.Combine(_installPath, "UYALauncher.exe");
        
        if (File.Exists(newLauncherPath)) {
            // Backup old launcher
            var backupPath = Path.Combine(_installPath, "UYALauncher.exe.old");
            if (File.Exists(oldLauncherPath)) {
                File.Delete(backupPath); // Delete old backup if exists
                File.Move(oldLauncherPath, backupPath);
            }
            
            // Move new launcher into place
            File.Move(newLauncherPath, oldLauncherPath);
            
            // Delete backup
            if (File.Exists(backupPath)) {
                File.Delete(backupPath);
            }
        }
    }

    private void UpdateStatus(string text, int progress) {
        Dispatcher.Invoke(() => {
            StatusText.Text = text;
            ProgressBar.Value = progress;
        });
    }
}
