using System;
using System.IO;
using System.Text.Json;

namespace UYALauncher;

public class PatchFlags
{
    public bool BootToMultiplayer { get; set; } = true;
    public bool Widescreen { get; set; } = true;
    public bool ProgressiveScan { get; set; } = true;
}

public class ConfigurationData
{
    public string Version { get; set; } = "3.0.0";
    public bool ShowConsole { get; set; } = false;
    public string IsoPath { get; set; } = string.Empty;
    public string Pcsx2Path { get; set; } = string.Empty;
    public string Region { get; set; } = "NTSC";
    public bool AutoUpdate { get; set; } = true;
    public bool EmbedWindow { get; set; } = true;
    public bool Fullscreen { get; set; } = true;
    public PatchFlags Patches { get; set; } = new();
}

public static class Configuration
{
    private const string ConfigFileName = "config.json";

    public static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "UYALauncher");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, ConfigFileName);
    }

    public static bool IsFirstRun()
    {
        var configPath = GetConfigPath();
        var exists = File.Exists(configPath);
        System.Diagnostics.Debug.WriteLine($"IsFirstRun: Config path '{configPath}' exists: {exists}");
        return !exists;
    }

    public static bool IsConfigComplete()
    {
        if (IsFirstRun())
        {
            System.Diagnostics.Debug.WriteLine("IsConfigComplete: IsFirstRun returned true");
            return false;
        }

        try
        {
            var config = Load();
            
            // Check that all required fields are present and not empty
            bool hasIso = !string.IsNullOrWhiteSpace(config.IsoPath);
            bool hasPcsx2 = !string.IsNullOrWhiteSpace(config.Pcsx2Path);
            bool hasRegion = !string.IsNullOrWhiteSpace(config.Region);
            
            System.Diagnostics.Debug.WriteLine($"IsConfigComplete Debug:");
            System.Diagnostics.Debug.WriteLine($"  IsoPath: '{config.IsoPath}' -> hasIso: {hasIso}");
            System.Diagnostics.Debug.WriteLine($"  Pcsx2Path: '{config.Pcsx2Path}' -> hasPcsx2: {hasPcsx2}");
            System.Diagnostics.Debug.WriteLine($"  Region: '{config.Region}' -> hasRegion: {hasRegion}");
            System.Diagnostics.Debug.WriteLine($"  Complete: {hasIso && hasPcsx2 && hasRegion}");
            
            return hasIso && hasPcsx2 && hasRegion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IsConfigComplete: Exception - {ex.Message}");
            return false;
        }
    }

    public static ConfigurationData Load()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            return new ConfigurationData();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<ConfigurationData>(json) ?? new ConfigurationData();
        }
        catch
        {
            return new ConfigurationData();
        }
    }

    public static void Save(ConfigurationData config)
    {
        var configPath = GetConfigPath();
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configPath, json);
    }

    public static string GetInstalledVersion()
    {
        try
        {
            var config = Load();
            return config.Version;
        }
        catch
        {
            return "0.0.0";
        }
    }

    public static void SetInstalledVersion(string version)
    {
        var config = Load();
        config.Version = version;
        Save(config);
    }
}
