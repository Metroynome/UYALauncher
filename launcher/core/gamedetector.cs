using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;

namespace UYALauncher;

public enum GameRegion {
    NTSC_U,  // North America  (SCUS, SLUS)
    NTSC_J,  // Japan          (SCPS, SLPS, SLAJ, PBPX, PAPX)
    NTSC_A,  // Asia           (SCAJ)
    NTSC_K,  // Korea / RAC4 JP alt slot (SCKA, SCKS, SLKA, SLKS, selected IDs)
    PAL,     // Europe / AU    (SCES, SLES, SCED, SLED)
    Unknown
}

public enum SupportedGame {
    Rac1,
    Rac2,
    Rac3,
    Rac4,
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
        GameRegion.NTSC_A => "NTSC-Asia",
        GameRegion.NTSC_K => "NTSC-K",
        GameRegion.PAL    => "PAL",
        _                 => "Unknown"
    };

    public string GameLabel => Game switch {
        SupportedGame.Rac1 => "Ratchet and Clank",
        SupportedGame.Rac2 => "Ratchet and Clank: Going Commando",
        SupportedGame.Rac3 => "Ratchet and Clank: Up Your Arsenal",
        SupportedGame.Rac4 => "Ratchet: Deadlocked",
        _                  => "Unknown"
    };

    public override string ToString() =>
        $"Game={GameLabel}, GameID={GameId}, Region={RegionLabel}";
}

public static class GameDisplayNames {
    public static string GetDisplayName(GameInfo? info, SupportedGame fallbackGame) {
        if (info == null)
            return GetFallbackName(fallbackGame);

        return info.Game switch {
            SupportedGame.Rac3 => info.Region == GameRegion.NTSC_U
                ? "Ratchet and Clank: Up Your Arsenal"
                : "Ratchet and Clank 3",
            SupportedGame.Rac4 => info.Region switch {
                GameRegion.PAL    => "Ratchet: Gladiator",
                GameRegion.NTSC_J => "Ratchet and Clank 4th",
                GameRegion.NTSC_A => "Ratchet and Clank 4th Special Gift Package",
                GameRegion.NTSC_K => "Ratchet and Clank: Gonggu Jeonsa Wigi Ilbal",
                _                 => "Ratchet: Deadlocked"
            },
            _ => info.GameLabel
        };
    }

    private static string GetFallbackName(SupportedGame game) {
        return game switch {
            SupportedGame.Rac3 => "Ratchet and Clank: Up Your Arsenal",
            SupportedGame.Rac4 => "Ratchet: Deadlocked",
            _                  => "Unknown"
        };
    }
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
        // RAC4 has Japanese/Asia discs that share region/version, so keep selected IDs separate.
        var normalizedGameId = gameId.ToUpperInvariant();
        if (normalizedGameId == "SCAJ-20157")
            return GameRegion.NTSC_A;

        if (normalizedGameId is "SCPS-15099" or "SCPS-19321")
            return GameRegion.NTSC_K;

        if (gameId.Length < 4)
            return GameRegion.Unknown;

        var prefix = normalizedGameId[..4];

        return prefix switch {
            "SCUS" or "SLUS" => GameRegion.NTSC_U,
            "SCES" or "SLES" or "SCED" or "SLED" => GameRegion.PAL,
            "SCPS" or "SLPS" or "SLAJ" or
            "PBPX" or "PAPX" => GameRegion.NTSC_J,
            "SCAJ" => GameRegion.NTSC_A,
            "SCKA" or "SCKS" or "SLKA" or "SLKS" => GameRegion.NTSC_K,
            _ => GameRegion.Unknown
        };
    }

    private static SupportedGame GameFromGameId(string gameId) {
        return gameId.ToUpperInvariant() switch {
            // Ratchet and Clank / RAC1
            "SCUS-97199" or "SCES-50916" or "SCED-50916" or
            "SCPS-15037" or "SCPS-19211" or "SCPS-19310" or
            "SCAJ-20001" or "PBPX-95516" => SupportedGame.Rac1,

            // Ratchet and Clank: Going Commando / RAC2
            "SCUS-97268" or "SCUS-97513" or "SCES-51607" or
            "SCPS-15056" or "SCPS-19302" or "SCPS-19317" or
            "SCAJ-20052" or "SCKA-20011" or "SCKA-20046" => SupportedGame.Rac2,

            // Ratchet and Clank: Up Your Arsenal / RAC3
            "SCUS-97353" or "SCUS-97518" or "SCES-52456" or
            "SCPS-15084" or "SCPS-19309" or "SCAJ-20109" or
            "SCKA-20037" => SupportedGame.Rac3,

            // Ratchet: Deadlocked / Ratchet: Gladiator / RAC4
            "SCUS-97465" or "SCES-53285" or "SCPS-15099" or
            "SCPS-15100" or "SCPS-19321" or "SCPS-19328" or
            "SCAJ-20157" or "SCKA-20060" or "SCKA-20108" => SupportedGame.Rac4,

            _ => SupportedGame.Unknown
        };
    }
}