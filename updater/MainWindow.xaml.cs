using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace UYALauncherUpdater;

public partial class MainWindow : Window
{
    private readonly string _installPath;
    private bool _launcherWasRunning;

    public MainWindow()
    {
        InitializeComponent();
        _installPath = AppDomain.CurrentDomain.BaseDirectory;
        Loaded += async (_, _) => await PerformUpdate();
    }

    private async Task PerformUpdate()
    {
        try
        {
            UpdateStatus("Checking for running launcher...", 10);
            await Task.Delay(400);

            var launcherPath = Path.Combine(_installPath, "UYALauncher.exe");

            // --- Kill running launcher ---
            var processes = Process.GetProcessesByName("UYALauncher");
            if (processes.Length > 0)
            {
                _launcherWasRunning = true;
                UpdateStatus("Closing launcher...", 20);

                foreach (var p in processes)
                {
                    try
                    {
                        p.Kill(true);
                        p.WaitForExit(5000);
                    }
                    catch { }
                }

                await Task.Delay(1500);
            }

            UpdateStatus("Extracting update files...", 40);
            await Task.Run(ExtractEmbeddedFiles);

            UpdateStatus("Verifying update...", 75);

            if (File.Exists(launcherPath))
            {
                var version = FileVersionInfo.GetVersionInfo(launcherPath).FileVersion;
                UpdateStatus($"Launching version {version}", 85);
            }

            await Task.Delay(800);

            // --- Relaunch launcher ---
            if (_launcherWasRunning && File.Exists(launcherPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launcherPath,
                    WorkingDirectory = _installPath,
                    UseShellExecute = true
                });
            }

            UpdateStatus("Finalizing...", 95);
            await Task.Delay(600);

            ScheduleSelfDelete();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Update failed:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Updater Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Application.Current.Shutdown();
        }
    }

    private void ExtractEmbeddedFiles()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames();

        foreach (var resource in resources)
        {
            string outputPath;

            // Handle launcher EXE (logical name is exactly "UYALauncher.exe")
            if (resource == "UYALauncher.exe")
            {
                outputPath = Path.Combine(_installPath, "UYALauncher.exe.new");
            }
            // Handle embedded data files (logical names start with "data/")
            else if (resource.StartsWith("data/"))
            {
                var relative = resource.Replace('/', Path.DirectorySeparatorChar);
                outputPath = Path.Combine(_installPath, relative);
            }
            else
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null) continue;

            using var file = File.Create(outputPath);
            stream.CopyTo(file);
        }

        ReplaceLauncherExecutable();
    }

    private void ReplaceLauncherExecutable()
    {
        var newPath = Path.Combine(_installPath, "UYALauncher.exe.new");
        var oldPath = Path.Combine(_installPath, "UYALauncher.exe");
        var backupPath = Path.Combine(_installPath, "UYALauncher.exe.old");

        if (!File.Exists(newPath))
            return;

        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(oldPath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    File.Move(oldPath, backupPath);
                }

                File.Move(newPath, oldPath);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        throw new Exception("Failed to replace launcher executable.");
    }

    private void ScheduleSelfDelete()
    {
        try
        {
            string updaterPath = Process.GetCurrentProcess().MainModule!.FileName!;

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C timeout 2 & del \"{updaterPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { }
    }

    private void UpdateStatus(string text, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            ProgressBar.Value = progress;
        });
    }
}