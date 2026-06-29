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

            var gameInfo = GameDetector.ReadFromIso(isoPath);
            var profile = GetMapProfile(gameInfo?.Game ?? SupportedGame.Unknown, region);
            var mapsDir = IOPath.Combine(isoDir, profile.LocalFolder);
            Directory.CreateDirectory(mapsDir);

            var progressWindow = new MapUpdateProgressWindow();
            progressWindow.Show();

            try
            {
                var totalMapsUpdated = 0;

                foreach (var source in profile.Sources)
                {
                    progressWindow.UpdateStatus($"Downloading {profile.DisplayName} {source.RegionName} map list...");

                    var maps = await GetMapListAsync(source.IndexFile);
                    var mapsToUpdate = GetMapsNeedingUpdate(maps, mapsDir, source.FileSuffix);

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

                        await DownloadMapAsync(map, mapsDir, source.FileSuffix, profile.RemoteFolder);
                    }

                    totalMapsUpdated += mapsToUpdate.Count;
                }

                try
                {
                    var versionUrl = $"{BaseUrl}/{profile.RemoteFolder}/version";
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

    private static MapProfile GetMapProfile(SupportedGame game, string region)
    {
        return game switch
        {
            SupportedGame.Deadlocked => new MapProfile(
                "Ratchet: Deadlocked",
                "dl",
                "dl",
                new[] { new MapSource("NTSC", "index_dl_ntsc.txt", "") }),

            _ => new MapProfile(
                "UYA",
                "uya",
                "uya",
                GetUyaSources(region))
        };
    }

    private static MapSource[] GetUyaSources(string region)
    {
        return region == "Both"
            ? new[] {
                new MapSource("NTSC", "index_uya_ntsc.txt", ""),
                new MapSource("PAL", "index_uya_pal.txt", ".pal")
            }
            : new[] {
                region == "PAL"
                    ? new MapSource("PAL", "index_uya_pal.txt", ".pal")
                    : new MapSource("NTSC", "index_uya_ntsc.txt", "")
            };
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
        string extension,
        string remoteFolder)
    {
        var filename = map.Filename + extension;
        var extensions = new[]
        {
            ".bg", ".thumb", ".map",
            ".world", ".sound", ".code", ".wad"
        };

        foreach (var ext in extensions)
        {
            var url = $"{BaseUrl}/{remoteFolder}/{filename}{ext}";
            var outputPath = IOPath.Combine(mapsDir, $"{filename}{ext}");

            try
            {
                var data = await Client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(outputPath, data);
            }
            catch
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        try
        {
            var versionUrl = $"{BaseUrl}/{remoteFolder}/{map.Filename}.version";
            var versionPath = IOPath.Combine(mapsDir, $"{map.Filename}.version");
            var versionData = await Client.GetByteArrayAsync(versionUrl);
            await File.WriteAllBytesAsync(versionPath, versionData);
        }
        catch
        {
            var versionPath = IOPath.Combine(mapsDir, $"{map.Filename}.version");
            await File.WriteAllBytesAsync(
                versionPath,
                BitConverter.GetBytes(map.Version));
        }
    }

    private sealed record MapProfile(
        string DisplayName,
        string RemoteFolder,
        string LocalFolder,
        IReadOnlyList<MapSource> Sources);

    private sealed record MapSource(
        string RegionName,
        string IndexFile,
        string FileSuffix);
}