using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UYALauncher;

public class PatchFlags {
    public bool BootToMultiplayer { get; set; } = true;
    public bool Widescreen { get; set; } = true;
}

public class ConfigurationData {
    public string Version { get; set; } = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "3.0.0";
    public bool ShowConsole { get; set; } = false;
    public string IsoPath { get; set; } = string.Empty;
    public string Rac3IsoPath { get; set; } = string.Empty;
    public string Rac4IsoPath { get; set; } = string.Empty;
    public string BiosPath { get; set; } = string.Empty;
    public string Region { get; set; } = "NTSC";
    public string DefaultGame { get; set; } = "None";
    public bool AutoUpdate { get; set; } = true;
    public bool EmbedWindow { get; set; } = true;
    public bool Fullscreen { get; set; } = true;
    public PatchFlags Patches { get; set; } = new();

    public void MigrateIsoPaths() {
        if (!string.IsNullOrWhiteSpace(IsoPath)) {
            var info = GameDetector.ReadFromIso(IsoPath);
            if (info?.Game == SupportedGame.Rac3 && string.IsNullOrWhiteSpace(Rac3IsoPath))
                Rac3IsoPath = IsoPath;
            else if (info?.Game == SupportedGame.Rac4 && string.IsNullOrWhiteSpace(Rac4IsoPath))
                Rac4IsoPath = IsoPath;
        }

        DefaultGame = NormalizeDefaultGame(DefaultGame);
        IsoPath = GetLaunchIsoPath();
    }

    public SupportedGame? GetDefaultLaunchGame() {
        return NormalizeDefaultGame(DefaultGame) switch {
            "Rac3" => SupportedGame.Rac3,
            "Rac4" => SupportedGame.Rac4,
            _      => null
        };
    }

    public string GetIsoPathForGame(SupportedGame game) {
        return game switch {
            SupportedGame.Rac3 => Rac3IsoPath,
            SupportedGame.Rac4 => Rac4IsoPath,
            _                  => string.Empty
        };
    }

    public string GetLaunchIsoPath() {
        var defaultGame = GetDefaultLaunchGame();
        if (defaultGame != null) {
            var defaultIsoPath = GetIsoPathForGame(defaultGame.Value);
            if (!string.IsNullOrWhiteSpace(defaultIsoPath))
                return defaultIsoPath;
        }

        if (!string.IsNullOrWhiteSpace(Rac4IsoPath))
            return Rac4IsoPath;

        if (!string.IsNullOrWhiteSpace(Rac3IsoPath))
            return Rac3IsoPath;

        return IsoPath;
    }

    public static string NormalizeDefaultGame(string? defaultGame) {
        return defaultGame?.Trim() switch {
            "Rac3" => "Rac3",
            "Rac4" => "Rac4",
            _      => "None"
        };
    }

    public IEnumerable<string> GetConfiguredIsoPaths() {
        bool yieldedAny = false;

        if (!string.IsNullOrWhiteSpace(Rac3IsoPath)) {
            yieldedAny = true;
            yield return Rac3IsoPath;
        }

        if (!string.IsNullOrWhiteSpace(Rac4IsoPath) &&
            !string.Equals(Rac4IsoPath, Rac3IsoPath, StringComparison.OrdinalIgnoreCase)) {
            yieldedAny = true;
            yield return Rac4IsoPath;
        }

        if (!yieldedAny && !string.IsNullOrWhiteSpace(IsoPath))
            yield return IsoPath;
    }
}

public static class Configuration {
    private const string ConfigFileName = "config.json";

    // --- Paths ---
    public static string GetAppDirectory() {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        return Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
    }

    public static string GetConfigPath() {
        var appDir = GetAppDirectory();
        var dataDir = Path.Combine(appDir, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, ConfigFileName);
    }

    public static string GetPcsx2Path() {
        var appDir = GetAppDirectory();
        return Path.Combine(appDir, "data", "emulator", "pcsx2-qt.exe");
    }

    public static string GetPatchesPath() {
        var appDir = GetAppDirectory();
        return Path.Combine(appDir, "data", "emulator", "patches");
    }

    // --- First run / config completeness ---
    public static bool IsFirstRun() {
        var configPath = GetConfigPath();
        return !File.Exists(configPath);
    }

    public static bool IsConfigComplete() {
        if (IsFirstRun()) {
            return false;
        }

        try {
            var config = Load();
            bool hasIso = !string.IsNullOrWhiteSpace(config.GetLaunchIsoPath());
            bool hasBios = !string.IsNullOrWhiteSpace(config.BiosPath);
            bool hasRegion = !string.IsNullOrWhiteSpace(config.Region);
            return hasIso && hasBios && hasRegion;
        } catch {
            return false;
        }
    }

    // --- Region normalization (FIXED) ---
    public static string NormalizeRegion(string? region) {
        if (string.IsNullOrWhiteSpace(region)) {
            return "NTSC";
        }

        var value = region.Trim().ToUpperInvariant();

        switch (value) {
            case "NTSC-U (NORTH AMERICA)":
            case "NTSC":
                return "NTSC";

            case "PAL (EUROPE)":
            case "PAL":
                return "PAL";

            case "BOTH":
                return "Both";

            default:
                // Preserve unknown values instead of forcing NTSC
                return region.Trim();
        }
    }

    // --- Load / Save ---
    public static ConfigurationData Load() {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath)) {
            return new ConfigurationData();
        }

        try {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigurationData>(json) ?? new ConfigurationData();
            config.Region = NormalizeRegion(config.Region);
            config.MigrateIsoPaths();
            return config;
        } catch {
            return new ConfigurationData();
        }
    }

    public static void Save(ConfigurationData config) {
        config.Region = NormalizeRegion(config.Region);
        config.MigrateIsoPaths();

        var configPath = GetConfigPath();
        var options = new JsonSerializerOptions {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configPath, json);
    }

    // --- Version helpers ---
    public static string GetInstalledVersion() {
        try {
            var config = Load();
            return config.Version;
        } catch {
            return "0.0.0";
        }
    }

    public static void SetInstalledVersion(string version) {
        var config = Load();
        config.Version = version;
        Save(config);
    }
}