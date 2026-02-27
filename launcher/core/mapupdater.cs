using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using IOPath = System.IO.Path;

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
    private static readonly HttpClient Client = new();

    public static async Task UpdateMapsAsync(string isoPath, string region)
    {
        try
        {
            var isoDir = IOPath.GetDirectoryName(isoPath);
            if (string.IsNullOrEmpty(isoDir))
                return;

            var mapsDir = IOPath.Combine(isoDir, "uya");
            Directory.CreateDirectory(mapsDir);

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
                    var mapsToUpdate = GetMapsNeedingUpdate(maps, mapsDir, extension);

                    if (mapsToUpdate.Count == 0)
                        continue;

                    progressWindow.SetTotalMaps(mapsToUpdate.Count + totalMapsUpdated);

                    for (int i = 0; i < mapsToUpdate.Count; i++)
                    {
                        var map = mapsToUpdate[i];
                        var currentMap = totalMapsUpdated + i + 1;
                        var totalMaps = mapsToUpdate.Count + totalMapsUpdated;

                        progressWindow.UpdateStatus(
                            $"Downloading {map.Mapname} ({currentMap}/{totalMaps})");

                        progressWindow.UpdateProgress(currentMap, totalMaps);

                        await DownloadMapAsync(map, mapsDir, extension);
                    }

                    totalMapsUpdated += mapsToUpdate.Count;
                }

                // Download global version file (PowerShell parity)
                try
                {
                    var versionUrl = $"{BaseUrl}/uya/version";
                    var versionPath = IOPath.Combine(mapsDir, "version");
                    var versionData = await Client.GetByteArrayAsync(versionUrl);
                    await File.WriteAllBytesAsync(versionPath, versionData);
                }
                catch { }

                if (totalMapsUpdated == 0)
                {
                    progressWindow.UpdateStatus("All maps are up to date!");
                    await Task.Delay(1000);
                }
                else
                {
                    progressWindow.UpdateStatus(
                        $"Update complete! ({totalMapsUpdated} maps updated)");
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
        var url = $"{BaseUrl}/{indexFile}";
        var content = await Client.GetStringAsync(url);

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

    private static List<MapInfo> GetMapsNeedingUpdate(
        List<MapInfo> maps,
        string mapsDir,
        string extension)
    {
        return maps.Where(map =>
        {
            var versionPath = IOPath.Combine(mapsDir, $"{map.Filename}.version");
            var localVersion = GetLocalMapVersion(versionPath);

            var filenameWithExt = map.Filename + extension;

            var worldPath = IOPath.Combine(mapsDir, $"{filenameWithExt}.world");
            var wadPath = IOPath.Combine(mapsDir, $"{filenameWithExt}.wad");

            bool missingFiles =
                !File.Exists(worldPath) &&
                !File.Exists(wadPath);

            return localVersion < map.Version || missingFiles;
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

    private static async Task DownloadMapAsync(
        MapInfo map,
        string mapsDir,
        string extension)
    {
        var filename = map.Filename + extension;
        var extensions = new[]
        {
            ".bg", ".thumb", ".map",
            ".world", ".sound", ".code", ".wad"
        };

        foreach (var ext in extensions)
        {
            var url = $"{BaseUrl}/uya/{filename}{ext}";
            var outputPath = IOPath.Combine(mapsDir, $"{filename}{ext}");

            try
            {
                var data = await Client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(outputPath, data);
            }
            catch
            {
                // Delete partial file if download failed
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        // Download version file
        try
        {
            var versionUrl = $"{BaseUrl}/uya/{map.Filename}.version";
            var versionPath = IOPath.Combine(mapsDir, $"{map.Filename}.version");
            var versionData = await Client.GetByteArrayAsync(versionUrl);
            await File.WriteAllBytesAsync(versionPath, versionData);
        }
        catch
        {
            // Fallback: create version file locally
            var versionPath = IOPath.Combine(mapsDir, $"{map.Filename}.version");
            await File.WriteAllBytesAsync(
                versionPath,
                BitConverter.GetBytes(map.Version));
        }
    }
}
