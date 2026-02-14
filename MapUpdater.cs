using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace UYALauncher;

public class MapInfo
{
    public required string Filename { get; init; }
    public required string Mapname { get; init; }
    public int Version { get; init; }
}

public static class MapUpdater
{
    private const string BaseUrl = "https://box.rac-horizon.com/downloads/maps";

    public static async Task UpdateMapsAsync(string isoPath, string region)
    {
        try
        {
            var isoDir = Path.GetDirectoryName(isoPath);
            if (string.IsNullOrEmpty(isoDir))
                return;

            var mapsDir = Path.Combine(isoDir, "uya");
            Directory.CreateDirectory(mapsDir);

            // Determine which regions to update
            var regions = region == "Both" 
                ? new[] { ("NTSC", ""), ("PAL", ".pal") }
                : new[] { (region, region == "PAL" ? ".pal" : "") };

            var progressWindow = new MapUpdateProgressWindow();
            progressWindow.Show();

            try
            {
                var totalMapsUpdated = 0;

                foreach (var (regionName, extension) in regions)
                {
                    var indexFile = regionName == "NTSC" 
                        ? "index_uya_ntsc.txt" 
                        : "index_uya_pal.txt";

                    progressWindow.UpdateStatus($"Downloading {regionName} map list...");

                    var maps = await GetMapListAsync(indexFile);
                    var mapsToUpdate = GetMapsNeedingUpdate(maps, mapsDir);

                    if (mapsToUpdate.Count == 0)
                        continue;

                    progressWindow.SetTotalMaps(mapsToUpdate.Count + totalMapsUpdated);

                    for (int i = 0; i < mapsToUpdate.Count; i++)
                    {
                        var map = mapsToUpdate[i];
                        var currentMap = totalMapsUpdated + i + 1;
                        var totalMaps = mapsToUpdate.Count + totalMapsUpdated;

                        progressWindow.UpdateStatus($"Downloading {map.Mapname} ({currentMap}/{totalMaps})");
                        progressWindow.UpdateProgress(currentMap, totalMaps);

                        await DownloadMapAsync(map, mapsDir, extension);
                    }

                    totalMapsUpdated += mapsToUpdate.Count;
                }

                if (totalMapsUpdated == 0)
                {
                    progressWindow.UpdateStatus("All maps are up to date!");
                    await Task.Delay(1000);
                }
                else
                {
                    progressWindow.UpdateStatus($"Update complete! ({totalMapsUpdated} maps updated)");
                    await Task.Delay(1500);
                }
            }
            finally
            {
                progressWindow.Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error updating maps:\n\n{ex.Message}",
                "Map Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static async Task<List<MapInfo>> GetMapListAsync(string indexFile)
    {
        using var client = new HttpClient();
        var url = $"{BaseUrl}/{indexFile}";
        var content = await client.GetStringAsync(url);

        var maps = new List<MapInfo>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Trim().Split('|');
            if (parts.Length < 3)
                continue;

            if (int.TryParse(parts[2], out int version))
            {
                maps.Add(new MapInfo
                {
                    Filename = parts[0],
                    Mapname = parts[1],
                    Version = version
                });
            }
        }

        return maps;
    }

    private static List<MapInfo> GetMapsNeedingUpdate(List<MapInfo> maps, string mapsDir)
    {
        return maps.Where(map =>
        {
            var versionPath = Path.Combine(mapsDir, $"{map.Filename}.version");
            var localVersion = GetLocalMapVersion(versionPath);
            return localVersion < map.Version;
        }).ToList();
    }

    private static int GetLocalMapVersion(string versionPath)
    {
        try
        {
            if (!File.Exists(versionPath))
                return -1;

            var bytes = File.ReadAllBytes(versionPath);
            if (bytes.Length >= 4)
                return BitConverter.ToInt32(bytes, 0);

            return -1;
        }
        catch
        {
            return -1;
        }
    }

    private static async Task DownloadMapAsync(MapInfo map, string mapsDir, string extension)
    {
        var filename = map.Filename + extension;
        var extensions = new[] { ".bg", ".thumb", ".map", ".world", ".sound", ".code", ".wad" };

        using var client = new HttpClient();

        foreach (var ext in extensions)
        {
            try
            {
                var url = $"{BaseUrl}/uya/{filename}{ext}";
                var outputPath = Path.Combine(mapsDir, $"{filename}{ext}");
                var data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(outputPath, data);
            }
            catch
            {
                // Some extensions might not exist for all maps
            }
        }

        // Download version file
        try
        {
            var versionUrl = $"{BaseUrl}/uya/{map.Filename}.version";
            var versionPath = Path.Combine(mapsDir, $"{map.Filename}.version");
            var versionData = await client.GetByteArrayAsync(versionUrl);
            await File.WriteAllBytesAsync(versionPath, versionData);
        }
        catch
        {
            // Create version file if download fails
            var versionPath = Path.Combine(mapsDir, $"{map.Filename}.version");
            await File.WriteAllBytesAsync(versionPath, BitConverter.GetBytes(map.Version));
        }
    }
}

public class MapUpdateProgressWindow : Window
{
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;

    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MapUpdateProgressWindow()
    {
        Title = "Updating Custom Maps";
        Width = 500;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        
        // Dark theme background
        Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
        Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        // Enable dark title bar
        Loaded += (s, e) => SetDarkTitleBar();

        var grid = new Grid();
        grid.Margin = new Thickness(20);

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

        _statusText = new TextBlock
        {
            Text = "Initializing...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            FontSize = 14
        };
        Grid.SetRow(_statusText, 0);
        grid.Children.Add(_statusText);

        _progressBar = new ProgressBar
        {
            Height = 24,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)), // Accent blue
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(1)
        };
        Grid.SetRow(_progressBar, 2);
        grid.Children.Add(_progressBar);

        Content = grid;
    }

    private void SetDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int value = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public void UpdateStatus(string status)
    {
        _statusText.Text = status;
    }

    public void SetTotalMaps(int total)
    {
        _progressBar.Maximum = total;
    }

    public void UpdateProgress(int current, int total)
    {
        _progressBar.Value = current;
    }
}
