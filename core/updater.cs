using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace UYALauncher;

public static class Updater
{
    private const string GitHubUser = "Metroynome";
    private const string GitHubRepo = "UYALauncher";
    private const string CurrentVersion = "3.0.0";

    public static async Task CheckAndUpdateAsync(bool silent)
    {
        try
        {
            var (updateAvailable, downloadUrl, remoteVersion) = await CheckForUpdateAsync();

            if (!updateAvailable)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        $"You are running the latest version ({CurrentVersion}).",
                        "Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            // Ask user if not silent
            if (!silent)
            {
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

            // Download update
            var tempPath = Path.GetTempPath();
            var newExePath = Path.Combine(tempPath, "UYALauncher_new.exe");

            var progressWindow = new UpdateProgressWindow();
            progressWindow.Show();

            try
            {
                await DownloadUpdateAsync(downloadUrl, newExePath, progressWindow);
            }
            catch (Exception ex)
            {
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

            // Launch self-update process
            var currentExe = Environment.ProcessPath ?? 
                           System.Reflection.Assembly.GetExecutingAssembly().Location;
            var args = $"--self-update \"{newExePath}|{remoteVersion}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                Arguments = args,
                UseShellExecute = true
            });

            // Exit current process
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(
                    $"Error checking for updates:\n\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private static async Task<(bool available, string downloadUrl, string version)> CheckForUpdateAsync()
    {
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

        // Get download URL for exe asset
        var downloadUrl = string.Empty;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name?.EndsWith(".exe") == true)
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            // Fallback to browser_download_url from release
            downloadUrl = root.GetProperty("browser_download_url").GetString() ?? string.Empty;
        }

        return (true, downloadUrl, tag);
    }

    private static async Task DownloadUpdateAsync(string url, string outputPath, UpdateProgressWindow progressWindow)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "UYALauncher");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        var totalRead = 0L;

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;

            if (totalBytes > 0)
            {
                var progress = (int)((totalRead * 100) / totalBytes);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    progressWindow.UpdateProgress(progress);
                });
            }
        }
    }

    public static void RunSelfUpdate(string newExePath, string version)
    {
        try
        {
            // Save version
            Configuration.SetInstalledVersion(version);

            var currentExe = Environment.ProcessPath ?? 
                           System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Wait for original process to exit
            System.Threading.Thread.Sleep(1000);

            // Replace exe
            if (File.Exists(currentExe))
                File.Delete(currentExe);

            File.Move(newExePath, currentExe);

            // Relaunch
            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Self-update failed:\n\n{ex.Message}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        Environment.Exit(0);
    }

    private static bool IsNewerVersion(string current, string remote)
    {
        var currentParts = ParseVersion(current);
        var remoteParts = ParseVersion(remote);

        for (int i = 0; i < 3; i++)
        {
            if (remoteParts[i] > currentParts[i])
                return true;
            if (remoteParts[i] < currentParts[i])
                return false;
        }

        return false;
    }

    private static int[] ParseVersion(string version)
    {
        var parts = new int[3];
        var segments = version.Trim().Split('.');

        for (int i = 0; i < Math.Min(segments.Length, 3); i++)
        {
            if (int.TryParse(segments[i], out int num))
                parts[i] = num;
        }

        return parts;
    }
}
