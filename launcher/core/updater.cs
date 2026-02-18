using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace UYALauncher;

public static class Updater {
    private const string GitHubUser = "Metroynome";
    private const string GitHubRepo = "UYALauncher";
    
    // Get version from assembly instead of hardcoding
    private static string CurrentVersion => System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "3.0.0";

    public static async Task CheckAndUpdateAsync(bool silent) {
        try {
            var (updateAvailable, updaterUrl, remoteVersion) = await CheckForUpdateAsync();

            if (!updateAvailable) {
                if (!silent) {
                    MessageBox.Show(
                        $"You are running the latest version ({CurrentVersion}).",
                        "Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            // If newer version exists but no updater, just notify
            if (string.IsNullOrEmpty(updaterUrl)) {
                if (!silent) {
                    MessageBox.Show(
                        $"A new version (v{remoteVersion}) is available, but no updater is included.\n\n" +
                        $"This may be a minor update. Visit GitHub to learn more.",
                        "Update Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            // Ask user if not silent
            if (!silent) {
                var result = MessageBox.Show(
                    $"Update available: v{remoteVersion}\n\n" +
                    $"Current version: v{CurrentVersion}\n" +
                    $"New version: v{remoteVersion}\n\n" +
                    "Download and install now?\n\n" +
                    "Note: This will close the launcher and PCSX2.",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Download updater
            var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UYALauncherUpdater.exe");

            var progressWindow = new UpdateProgressWindow();
            progressWindow.Show();

            try {
                await DownloadUpdateAsync(updaterUrl, updaterPath, progressWindow);
            } catch (Exception ex) {
                progressWindow.Close();
                MessageBox.Show(
                    $"Failed to download update:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            progressWindow.Close();

            // Terminate PCSX2
            PCSX2Manager.Terminate();

            // Launch updater
            Process.Start(new ProcessStartInfo {
                FileName = updaterPath,
                UseShellExecute = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            });

            // Exit current process
            Application.Current.Shutdown();
        } catch (Exception ex) {
            if (!silent) {
                MessageBox.Show(
                    $"Error checking for updates:\n\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private static async Task<(bool available, string updaterUrl, string version)> CheckForUpdateAsync() {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UYALauncher");

        var url = $"https://api.github.com/repos/{GitHubUser}/{GitHubRepo}/releases/latest";
        var response = await client.GetStringAsync(url);

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "0.0.0";
        if (tag.StartsWith("v"))
            tag = tag.Substring(1);

        if (!IsNewerVersion(CurrentVersion, tag))
            return (false, string.Empty, tag);

        // Look for UYALauncherUpdater.exe in assets
        var updaterUrl = string.Empty;
        if (root.TryGetProperty("assets", out var assets)) {
            foreach (var asset in assets.EnumerateArray()) {
                var name = asset.GetProperty("name").GetString();
                if (name == "UYALauncherUpdater.exe") {
                    updaterUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                    break;
                }
            }
        }

        // Return true for update available, but empty URL if no updater found
        return (true, updaterUrl, tag);
    }

    private static async Task DownloadUpdateAsync(string url, string outputPath, UpdateProgressWindow progressWindow) {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UYALauncher");

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
                var progress = (int)((totalRead * 100) / totalBytes);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    progressWindow.UpdateProgress(progress);
                });
            }
        }
    }

    private static bool IsNewerVersion(string current, string remote) {
        var currentParts = ParseVersion(current);
        var remoteParts = ParseVersion(remote);

        for (int i = 0; i < 3; i++) {
            if (remoteParts[i] > currentParts[i])
                return true;
            if (remoteParts[i] < currentParts[i])
                return false;
        }

        return false;
    }

    private static int[] ParseVersion(string version) {
        var parts = new int[3];
        var segments = version.Trim().Split('.');

        for (int i = 0; i < Math.Min(segments.Length, 3); i++) {
            if (int.TryParse(segments[i], out int num))
                parts[i] = num;
        }

        return parts;
    }
}