using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UYALauncherWebInstall;

public class Program {
    [STAThread]
    public static void Main() {
        var app = new Application();
        app.Startup += (s, e) => {
            // Catch any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Fatal error:\n\n{ex?.Message}", 
                    "Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        };
        
        app.Run(new InstallerWindow());
    }
}

public class InstallerWindow : Window {
    private const string GitHubUser = "Metroynome";
    private const string GitHubRepo = "UYALauncher";
    
    private TextBlock _statusText;
    private ProgressBar _progressBar;

    public InstallerWindow() {
        // Window properties
        Title = "UYA Launcher - Web Installer";
        Width = 500;
        Height = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        
        // Build UI
        Content = CreateUI();
        
        // Ensure window shows on top
        Topmost = true;
        Activate();
        Topmost = false;
        
        // Start download
        Loaded += async (s, e) => await DownloadAndRunInstaller();
    }

    private UIElement CreateUI() {
        // Border
        var border = new Border {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32))
        };

        // Main grid
        var grid = new Grid { Margin = new Thickness(30) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Spacer
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Description
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Spacer
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // Spacer
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress

        // Title
        var title = new TextBlock {
            Text = "UYA Launcher Web Installer",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(title, 0);
        grid.Children.Add(title);

        // Description
        var description = new TextBlock {
            Text = "This will download the latest UYA Launcher installer\nfrom GitHub and run it.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        Grid.SetRow(description, 2);
        grid.Children.Add(description);

        // Status text
        _statusText = new TextBlock {
            Text = "Ready to download...",
            FontSize = 14,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(_statusText, 4);
        grid.Children.Add(_statusText);

        // Progress bar
        _progressBar = new ProgressBar {
            Height = 24,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(61, 61, 61)),
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };
        Grid.SetRow(_progressBar, 6);
        grid.Children.Add(_progressBar);

        border.Child = grid;
        return border;
    }

    private async Task DownloadAndRunInstaller() {
        try {
            UpdateStatus("Checking for latest release...", 10);
            
            var installerUrl = await GetLatestInstallerUrl();
            
            if (string.IsNullOrEmpty(installerUrl)) {
                MessageBox.Show(
                    "Could not find UYALauncherSetup.exe in the latest release.\n\n" +
                    "Please visit GitHub to download manually.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            UpdateStatus("Downloading installer...", 20);
            
            var tempPath = Path.Combine(Path.GetTempPath(), "UYALauncherSetup.exe");
            await DownloadFileAsync(installerUrl, tempPath);

            UpdateStatus("Launching installer...", 95);
            await Task.Delay(500);

            Process.Start(new ProcessStartInfo {
                FileName = tempPath,
                UseShellExecute = true
            });

            UpdateStatus("Complete!", 100);
            await Task.Delay(500);

            Application.Current.Shutdown();
        } catch (Exception ex) {
            MessageBox.Show(
                $"Failed to download installer:\n\n{ex.Message}\n\n" +
                "Please visit GitHub to download manually.",
                "Download Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private async Task<string> GetLatestInstallerUrl() {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UYALauncherWebInstall");

        var url = $"https://api.github.com/repos/{GitHubUser}/{GitHubRepo}/releases/latest";
        var response = await client.GetStringAsync(url);

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("assets", out var assets)) {
            foreach (var asset in assets.EnumerateArray()) {
                var name = asset.GetProperty("name").GetString();
                if (name == "UYALauncherSetup.exe") {
                    return asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private async Task DownloadFileAsync(string url, string outputPath) {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UYALauncherWebInstall");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        var totalRead = 0L;

        while (true) {
            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;

            if (totalBytes > 0) {
                var progress = 20 + (int)((totalRead * 75) / totalBytes);
                await Dispatcher.InvokeAsync(() => {
                    UpdateStatus($"Downloading... {totalRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB", progress);
                });
            }
        }
    }

    private void UpdateStatus(string text, int progress) {
        _statusText.Text = text;
        _progressBar.Value = progress;
    }
}
