using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;

namespace UYALauncher;

public enum GameRegion {
    NTSC_U,  // North America  (SCUS, SLUS)
    NTSC_J,  // Japan          (SCPS, SLPS, SCAJ, SLAJ)
    NTSC_K,  // Korea          (SCKS, SLKS)
    PAL,     // Europe / AU    (SCES, SLES, SCPS-5xxxx range handled separately)
    Unknown
}

public enum SupportedGame {
    UpYourArsenal,
    Deadlocked,
    Unknown
}

public class GameInfo {
    public string GameId { get; init; } = string.Empty;
    public string RawBootLine { get; init; } = string.Empty;
    public GameRegion Region { get; init; } = GameRegion.Unknown;
    public SupportedGame Game { get; init; } = SupportedGame.Unknown;

    /// <summary>
    /// Human-readable region label, e.g. "NTSC-U", "PAL", "NTSC-J".
    /// </summary>
    public string RegionLabel => Region switch {
        GameRegion.NTSC_U => "NTSC-U",
        GameRegion.NTSC_J => "NTSC-J",
        GameRegion.NTSC_K => "NTSC-K",
        GameRegion.PAL    => "PAL",
        _                 => "Unknown"
    };

    public string GameLabel => Game switch {
        SupportedGame.UpYourArsenal => "Ratchet & Clank: Up Your Arsenal",
        SupportedGame.Deadlocked    => "Ratchet: Deadlocked",
        _                           => "Unknown"
    };

    public override string ToString() =>
        $"Game={GameLabel}, GameID={GameId}, Region={RegionLabel}";
}

public static class GameDetector {
    // SYSTEM.CNF boot line looks like: BOOT2 = cdrom0:\SCUS_973.53;1
    private static readonly Regex BootRegex = new(
        @"BOOT2\s*=\s*cdrom0:\\(?<file>[A-Z0-9_]+\.[A-Z0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Reads the ISO at <paramref name="isoPath"/> and returns a <see cref="GameInfo"/>
    /// describing the disc, or <c>null</c> if the file cannot be read or has no SYSTEM.CNF.
    /// </summary>
    public static GameInfo? ReadFromIso(string isoPath) {
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            return null;

        try {
            using var isoStream = File.OpenRead(isoPath);
            var cd = new CDReader(isoStream, joliet: true);

            // SYSTEM.CNF may appear with or without leading backslash depending on DiscUtils
            string? cnfContent = TryReadFile(cd, @"SYSTEM.CNF")
                               ?? TryReadFile(cd, @"\SYSTEM.CNF");

            if (cnfContent == null)
                return null;

            return ParseSystemCnf(cnfContent);
        } catch (Exception ex) {
            Console.WriteLine($"[GameDetector] Error reading ISO: {ex.Message}");
            return null;
        }
    }

    private static string? TryReadFile(CDReader cd, string path) {
        try {
            if (!cd.FileExists(path))
                return null;

            using var stream = cd.OpenFile(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            return reader.ReadToEnd();
        } catch {
            return null;
        }
    }

    private static GameInfo ParseSystemCnf(string cnfContent) {
        var match = BootRegex.Match(cnfContent);
        if (!match.Success)
            return new GameInfo { RawBootLine = string.Empty };

        var bootLine = match.Value.Trim();
        var gameId   = NormaliseGameId(match.Groups["file"].Value);
        var region   = RegionFromGameId(gameId);
        var game     = GameFromGameId(gameId);

        return new GameInfo {
            GameId      = gameId,
            RawBootLine = bootLine,
            Region      = region,
            Game        = game
        };
    }

    /// <summary>
    /// Converts a raw filename like "SCUS_973.53" to the canonical disc-ID
    /// format "SCUS-97353".
    /// </summary>
    private static string NormaliseGameId(string raw) {
        var parts = raw.Split('_', 2);
        if (parts.Length != 2)
            return raw.ToUpperInvariant();

        var prefix = parts[0].ToUpperInvariant();
        var digits = parts[1].Replace(".", string.Empty);

        return $"{prefix}-{digits}";
    }

    private static GameRegion RegionFromGameId(string gameId) {
        if (gameId.Length < 4)
            return GameRegion.Unknown;

        var prefix = gameId[..4].ToUpperInvariant();

        return prefix switch {
            "SCUS" or "SLUS" => GameRegion.NTSC_U,
            "SCES" or "SLES" or "SCED" or "SLED" => GameRegion.PAL,
            "SCPS" or "SLPS" or "SCAJ" or "SLAJ" or
            "PBPX" or "PAPX" => GameRegion.NTSC_J,
            "SCKS" or "SLKS" => GameRegion.NTSC_K,
            _ => GameRegion.Unknown
        };
    }

    private static SupportedGame GameFromGameId(string gameId) {
        return gameId.ToUpperInvariant() switch {
            "SCUS-97353" or "SCES-52456" => SupportedGame.UpYourArsenal,
            "SCUS-97465" or "SCES-53285" => SupportedGame.Deadlocked,
            _                            => SupportedGame.Unknown
        };
    }
}